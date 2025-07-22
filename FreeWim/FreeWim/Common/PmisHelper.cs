using System.Data;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Dapper;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using FreeWim.Models.PmisAndZentao;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FreeWim.Common;

public class PmisHelper(IConfiguration configuration, ILogger<ZentaoHelper> logger, PushMessageHelper pushMessageHelper, TokenService tokenService, IChatClient chatClient)
{
    /// <summary>
    /// 查询日报列表
    /// </summary>
    /// <returns></returns>
    public JObject QueryMyByDate()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(pmisInfo.Url + "/unioa/job/userWork/queryMy", new
        {
            index = 1,
            size = 30,
            conditions = new object[] { },
            order = new object[] { },
            data = new
            {
                status = (object)null,
                hasFile = (object)null,
                time = new object[] { }
            }
        }, new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } }).Result;
        var json = JObject.Parse(postResponse.Content.ReadAsStringAsync().Result);
        //var result = JsonSerializer.Deserialize<QueryMyByDateOutput>(postResponse.Content.ReadAsStringAsync().Result);
        return json;
    }

    /// <summary>
    /// 根据日期及用户ID获取每日工作计划明细
    /// </summary>
    /// <param name="fillDate"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public JObject QueryWorkDetailByDate(string fillDate, string userId)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var getResponse = httpHelper.GetAsync(pmisInfo.Url + $"/unioa/job/userWork/getByDateAndUserId?fillDate={fillDate}&userId={userId}&type=0",
            new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } }).Result;
        var json = JObject.Parse(getResponse.Content.ReadAsStringAsync().Result);
        //var result = JsonSerializer.Deserialize<GetByDateAndUserIdResponse>(getResponse.Content.ReadAsStringAsync().Result);
        return json;
    }

    /// <summary>
    /// 提交工作日报
    /// </summary>
    /// <param name="fillDate"></param>
    /// <param name="userId"></param>
    public PMISInsertResponse CommitWorkLogByDate(string fillDate, string userId)
    {
        /*
         * target:衡量目标
         * planFinishAct:计划完成成果
         * realJob:实际从事工作与成果
         * responsibility:所属职责
         * workType:工作分类     planfinishact   realjob
         */
        try
        {
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var finishCount = 0;
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
            var httpHelper = new HttpRequestHelper();
            var workLogBody = QueryWorkDetailByDate(fillDate, userId);
            workLogBody["Response"]!["status"] = 1;
            if (workLogBody["Response"]?["details"] is JArray dataArray)
            {
                var ztTaskIds = dataArray
                    .Select(item => item["ztTaskId"]?.ToString())
                    .Where(idStr => int.TryParse(idStr, out _)) // 过滤无效值
                    .Select(idStr => int.Parse(idStr!)) // 安全解析为 int
                    .Distinct()
                    .ToArray();
                var zenTaoList = dbConnection.Query(@"select id,target,planfinishact,realjob from public.zentaotask WHERE ID = ANY(:id)", new { id = ztTaskIds })
                    .ToDictionary(row => (string)row.id.ToString(), row => new
                    {
                        Target = (string)row.target,
                        PlanFinishAct = (string)row.planfinishact,
                        RealJob = (string)row.realjob
                    });
                foreach (var jToken in dataArray)
                {
                    var item = (JObject)jToken;
                    item["target"] = zenTaoList.ContainsKey(jToken["ztTaskId"]?.ToString()) ? zenTaoList[jToken["ztTaskId"]?.ToString()].Target : item["description"];
                    item["planFinishAct"] = zenTaoList.ContainsKey(jToken["ztTaskId"]?.ToString()) ? zenTaoList[jToken["ztTaskId"]?.ToString()].PlanFinishAct : item["description"];
                    item["responsibility"] = pmisInfo.WorkContent;
                    item["workType"] = pmisInfo.WorkType;
                    item["realJob"] = zenTaoList.ContainsKey(jToken["ztTaskId"]?.ToString()) ? zenTaoList[jToken["ztTaskId"]?.ToString()].RealJob : item["description"];
                    finishCount++;
                }
            }

            var res = workLogBody["Response"]?.ToString(Formatting.None);
            var postRespone = httpHelper.PostAsyncStringBody(pmisInfo?.Url + "/unioa/job/userWork/insert", workLogBody["Response"]?.ToString(Formatting.None),
                    new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } })
                .Result;
            var result = JsonSerializer.Deserialize<PMISInsertResponse>(postRespone.Content.ReadAsStringAsync().Result);
            if (result.Success) pushMessageHelper.Push("日报", $"{DateTime.Now:yyyy-MM-dd}已发送\n今日完成" + finishCount + " 条任务", PushMessageHelper.PushIcon.Note);
            else pushMessageHelper.Push("日报错误", postRespone.Content.ReadAsStringAsync().Result, PushMessageHelper.PushIcon.Alert);
            return result;
        }
        catch (Exception e)
        {
            pushMessageHelper.Push("提交日报异常", e.Message, PushMessageHelper.PushIcon.Alert);
            logger.LogError("提交日报异常:" + e.Message);
            return new PMISInsertResponse();
        }
    }

    /// <summary>
    /// 通过项目编码获取PMIS项目信息
    /// </summary>
    /// <param name="projectCode"></param>
    /// <returns></returns>
    public ProjectInfo GetProjectInfo(string projectCode)
    {
        var projectInfo = new ProjectInfo();
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var requestObject = new
        {
            url = pmisInfo.Url + "/hddev/form/formobjectdata/project_query:1/query.json",
            body = new
            {
                index = 1,
                size = -1,
                conditions = new[]
                {
                    new
                    {
                        Field = "contract_id",
                        Value = projectCode,
                        Operate = "like",
                        Relation = "or"
                    },
                    new
                    {
                        Field = "contract_unit",
                        Value = projectCode,
                        Operate = "like",
                        Relation = "or"
                    },
                    new
                    {
                        Field = "project_name",
                        Value = projectCode,
                        Operate = "like",
                        Relation = "or"
                    }
                },
                order = Array.Empty<object>(),
                authority = new
                {
                    tenantIds = (object?)null
                },
                conditionsSql = Array.Empty<object>()
            }
        };
        var postRespone = httpHelper.PostAsync(pmisInfo.Url + "/hddev/sys/sysinterface/externalInterface/post", requestObject,
                new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        if (projectJson["Response"] != null)
            if (projectJson["Response"]?["rows"] is JArray dataArray)
            {
                var jToken = dataArray.First();
                projectInfo.contract_id = jToken["contract_id"]!.ToString();
                projectInfo.contract_unit = jToken["contract_unit"]!.ToString();
                projectInfo.project_name = jToken["project_name"]!.ToString();
            }

        return projectInfo;
    }

    /// <summary>
    /// 创建加班第一步
    /// </summary>
    /// <param name="projectInfo"></param>
    /// <param name="orderNo"></param>
    /// <returns></returns>
    public string OvertimeWork_Insert(ProjectInfo projectInfo, string orderNo, string Content)
    {
        var id = string.Empty;
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        //var projectInfo = GetProjectInfo(projectCode);
        var requestObject = new Dictionary<string, object?>
        {
            { "child_groups", new object[] { } },
            { "user_sn", pmisInfo.UserAccount },
            { "pms_pushed", "0" },
            { "work_date", DateTime.Now.ToString("yyyy-MM-dd") },
            { "user_id$$text", pmisInfo.UserName },
            { "user_id", pmisInfo.UserId },
            { "org_id$$text", "管网产品组" },
            { "org_id", "67" },
            { "work_overtime_type", "1" },
            { "work_type", "1" },
            { "contract_id", projectInfo.contract_id },
            { "contract_unit", projectInfo.contract_unit },
            { "project_name", projectInfo.project_name },
            { "position", "1" },
            { "plan_start_time", DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime },
            { "plan_end_time", DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime },
            {
                "plan_work_overtime_hour",
                (DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime) - DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime)).TotalHours
            },
            { "subject_matter", Content },
            { "reason", "1" },
            { "order_no", orderNo },
            { "remark", "" },
            { "work_overtime_type$$text", "延时加班" },
            { "work_type$$text", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "产品开发/测试/设计" : "项目开发/测试/设计" },
            { "position$$text", "公司" },
            { "reason$$text", "上线支撑" },
            { "product_name", null } // 明确声明为 null
        };
        var json = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
        var postRespone = httpHelper.PostAsync(pmisInfo.Url + "/hddev/form/formobjectdata/oa_workovertime_plan_apply:7/insert.json", requestObject,
                new Dictionary<string, string> { { "token", tokenService.GetTokenAsync() }, { "uniwaterutoken", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        if (projectJson["Response"] == null) return id;
        if (projectJson["Response"]!["id"] != null)
            id = projectJson["Response"]!["id"]!.ToString();

        return id;
    }

    /// <summary>
    /// 创建加班第二步
    /// </summary>
    /// <param name="projectInfo"></param>
    /// <param name="id"></param>
    /// <param name="orderNo"></param>
    /// <returns></returns>
    public string OvertimeWork_CreateOrder(ProjectInfo projectInfo, string id, string orderNo, string Content)
    {
        var ProcessId = string.Empty;
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var requestObject = new Dictionary<string, object?>
        {
            { "child_groups", new object[] { } },
            { "user_sn", pmisInfo.UserAccount },
            { "pms_pushed", "0" },
            { "work_date", DateTime.Now.ToString("yyyy-MM-dd") },
            { "user_id$$text", pmisInfo.UserName },
            { "user_id", pmisInfo.UserId },
            { "org_id$$text", "管网产品组" },
            { "org_id", "67" },
            { "work_overtime_type", "1" },
            { "work_type", "1" },
            { "contract_id", projectInfo.contract_id },
            { "contract_unit", projectInfo.contract_unit },
            { "project_name", projectInfo.project_name },
            { "position", "1" },
            { "plan_start_time", DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime },
            { "plan_end_time", DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime },
            {
                "plan_work_overtime_hour",
                (DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime) - DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime)).TotalHours
            },
            { "subject_matter", Content },
            { "reason", "1" },
            { "order_no", orderNo },
            { "remark", "" },
            { "work_overtime_type$$text", "延时加班" },
            { "work_type$$text", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "产品开发/测试/设计" : "项目开发/测试/设计" },
            { "position$$text", "公司" },
            { "reason$$text", "上线支撑" },
            { "product_name", null }, // null 明确声明
            { "creator_gid", "67" },
            { "creator_gnm", "管网产品组" },
            { "creator_id", pmisInfo.UserId },
            { "creator_nm", pmisInfo.UserName },
            { "creator_duty", null }, // null 明确声明
            { "creator_mobile", pmisInfo.UserMobile },
            { "creator_sn", pmisInfo.UserAccount },
            { "id", id },
            { "", "" }, // 注意：空字段名称
            { "$$formHtmlId", "b187562cecea44598d9cdbe2bf5efc42" },
            { "$$saveType", "N" },
            { "$$saveFields", "" },
            { "$$objectPK", "id" }
        };

        var json = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
        var postRespone = httpHelper.PostAsync(pmisInfo.Url + "/bpm/customize-api/jiaban_test/create-order2", requestObject,
                new Dictionary<string, string> { { "token", tokenService.GetTokenAsync() }, { "uniwaterutoken", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        if (projectJson["Response"] == null) return id;
        if (projectJson["Response"]!["id"] != null)
            ProcessId = projectJson["Response"]!["id"]!.ToString();

        return ProcessId;
    }

    /// <summary>
    /// 查询工单
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public JObject OvertimeWork_Query(string id)
    {
        //var id = string.Empty;
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var requestObject = new
        {
            conditions = new[]
            {
                new
                {
                    Field = "id",
                    Value = id,
                    Operate = "=",
                    Relation = "and"
                }
            },
            order = new object[] { },
            index = 1,
            size = 20000
        };

        var json = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
        var postRespone = httpHelper.PostAsync(pmisInfo.Url + "/hddev/form/formobjectdata/oa_workovertime_plan_apply:7/query.json", requestObject,
                new Dictionary<string, string> { { "token", tokenService.GetTokenAsync() }, { "uniwaterutoken", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        return projectJson;
    }

    /// <summary>
    /// 创建加班第三步
    /// </summary>
    /// <param name="projectInfo"></param>
    /// <param name="id"></param>
    /// <param name="orderNo"></param>
    /// <param name="processId"></param>
    /// <returns></returns>
    public JObject OvertimeWork_Update(ProjectInfo projectInfo, string id, string orderNo, string processId, string Content)
    {
        //var id = string.Empty;
        var processInfo = OvertimeWork_Query(id);
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var requestObject = new Dictionary<string, object?>
        {
            { "child_groups", new object[] { } },
            { "user_sn", pmisInfo.UserAccount },
            { "pms_pushed", "0" },
            { "work_date", DateTime.Now.ToString("yyyy-MM-dd") },
            { "user_id$$text", pmisInfo.UserName },
            { "user_id", pmisInfo.UserId },
            { "org_id$$text", "管网产品组" },
            { "org_id", "67" },
            { "work_overtime_type", "1" },
            { "work_type", "1" },
            { "contract_id", projectInfo.contract_id },
            { "contract_unit", projectInfo.contract_unit },
            { "project_name", projectInfo.project_name },
            { "position", "1" },
            { "plan_start_time", DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime },
            { "plan_end_time", DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime },
            {
                "plan_work_overtime_hour",
                (DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime) - DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime)).TotalHours
            },
            { "subject_matter", Content },
            { "reason", "1" },
            { "order_no", orderNo },
            { "remark", "" },
            { "work_overtime_type$$text", "延时加班" },
            { "work_type$$text", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "产品开发/测试/设计" : "项目开发/测试/设计" },
            { "position$$text", "公司" },
            { "reason$$text", "上线支撑" },
            { "id", id },
            { "", "" },
            { "$$createProcessFlag", 1 },
            { "$$createProcessId", processId },
            { "hddev_proc_task", processInfo["hddev_proc_task"] },
            { "hddev_proc_status", processInfo["hddev_proc_status"] },
            { "hddev_proc_task_code", processInfo["hddev_proc_task_code"] },
            { "hddev_business_key", processInfo["hddev_business_key"] },
            { "product_name", null }
        };
        var json = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
        var postRespone = httpHelper.PostAsync(pmisInfo.Url + "/hddev/form/formobjectdata/oa_workovertime_plan_apply:7/update.json", requestObject,
                new Dictionary<string, string> { { "token", tokenService.GetTokenAsync() }, { "uniwaterutoken", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        return projectJson;
    }

    /// <summary>
    /// 查询周报列表
    /// </summary>
    /// <returns></returns>
    public JObject QueryMyByWeek()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(pmisInfo.Url + "/unioa/job/weekWork/queryMy", new
        {
            index = 1,
            size = 30,
            conditions = new object[] { },
            order = new object[] { },
            data = new
            {
                status = (string?)null,
                hasFile = (string?)null,
                timeList = new[]
                {
                    DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                    DateTime.Now.AddMonths(1).ToString("yyyy-MM-dd")
                },
                beginDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                endDate = DateTime.Now.AddMonths(1).ToString("yyyy-MM-dd")
            }
        }, new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } }).Result;
        var json = JObject.Parse(postResponse.Content.ReadAsStringAsync().Result);
        return json;
    }

    /// <summary>
    /// 根据日期及用户ID获取每周工作计划明细
    /// </summary>
    /// <param name="weekDayInfo"></param>
    /// <returns></returns>
    public JObject QueryWorkDetailByWeek(WeekDayInfo weekDayInfo)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var getResponse = httpHelper.PostAsync(pmisInfo.Url + $"/unioa/job/userWork/queryMyCommit", new
            {
                index = 1,
                size = 30,
                conditions = new object[] { },
                order = new object[] { },
                data = new
                {
                    beginDate = weekDayInfo.StartOfWeek,
                    endDate = weekDayInfo.EndOfWeek,
                    userId = pmisInfo.UserId
                }
            },
            new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } }).Result;
        var json = JObject.Parse(getResponse.Content.ReadAsStringAsync().Result);
        return json;
    }

    /// <summary>
    /// 根据日期及用户ID获取周报提交参数
    /// </summary>
    /// <param name="weekDayInfo"></param>
    /// <returns></returns>
    public JObject QueryWorkByWeek(WeekDayInfo weekDayInfo)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var getResponse = httpHelper.GetAsync(pmisInfo.Url + $"/unioa/job/weekWork/getByDateAndUserId?fillDate={weekDayInfo.StartOfWeek}&userId={pmisInfo.UserId}",
            new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } }).Result;
        var json = JObject.Parse(getResponse.Content.ReadAsStringAsync().Result);
        return json;
    }

    /// <summary>
    /// 提交工作周报
    /// </summary>
    /// <param name="weekDayInfo"></param>
    /// <returns></returns>
    public PMISInsertResponse CommitWorkLogByWeek(WeekDayInfo weekDayInfo)
    {
        var result = new PMISInsertResponse { Success = false };
        try
        {
            var finishCount = 0;
            var httpHelper = new HttpRequestHelper();
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var repeat = false;
            //判断是否已提交过周报
            var weekList = QueryMyByWeek();
            if (bool.Parse(weekList["Success"]?.ToString()!))
                if (weekList["Response"]!["rows"] is JArray dataArray)
                    foreach (var jToken in dataArray)
                    {
                        var item = (JObject)jToken;
                        if (item["fillWeek"]!.ToString() == DateTime.Now.Year + "-" + weekDayInfo.WeekNumber)
                        {
                            repeat = true;
                            break;
                        }
                    }

            if (!repeat)
            {
                //组装本周工作内容
                var workContent = string.Empty;
                var workDetail = QueryWorkDetailByWeek(weekDayInfo);
                if (bool.Parse(workDetail["Success"]?.ToString()!))
                    if (workDetail["Response"]!["rows"] is JArray dataArray)
                    {
                        finishCount = dataArray.Count;
                        var ztTaskIds = dataArray
                            .Select(item => item["ztTaskId"]?.ToString())
                            .Where(idStr => int.TryParse(idStr, out _)) // 过滤无效值
                            .Select(idStr => int.Parse(idStr!)) // 安全解析为 int
                            .Distinct()
                            .ToArray();
                        var zenTaoList = dbConnection.Query(@"select id,executionname from public.zentaotask WHERE ID = ANY(:id)", new { id = ztTaskIds })
                            .ToDictionary(row => (string)row.id.ToString(), row => (string?)row.executionname ?? "");
                        foreach (var workItem in dataArray)
                            if (zenTaoList.ContainsKey(workItem["ztTaskId"].ToString()))
                                workContent += zenTaoList[workItem["ztTaskId"].ToString()] + "工作内容：" + workItem["taskName"] + "," + workItem["description"] + ";";
                            else
                                workContent += workItem["taskName"] + "," + workItem["description"] + ";";
                    }

                if (!string.IsNullOrEmpty(workContent))
                {
                    //deepseek润色工作总结
                    var chatOptions = new ChatOptions { Tools = [] };
                    var chatHistory = new List<ChatMessage>
                    {
                        new(ChatRole.System, pmisInfo.WeekPrompt),
                        new(ChatRole.User, workContent)
                    };
                    var deepSeekRes = chatClient.GetResponseAsync(chatHistory, chatOptions).Result;
                    var deepSeekContent = deepSeekRes.Text;
                    if (!string.IsNullOrEmpty(deepSeekContent))
                    {
                        var workByWeek = QueryWorkByWeek(weekDayInfo);
                        if (bool.Parse(workByWeek["Success"]?.ToString()!))
                        {
                            var workWeekBody = workByWeek["Response"];
                            workWeekBody["status"] = 1;
                            workWeekBody["workSummary"] = deepSeekContent;
                            workWeekBody["recipientId"] = "6332da1056a7b316e0574816";
                            workWeekBody["recipientName"] = "陈云";
                            workWeekBody["details"] = new JArray(new string[] { });
                            var postRespone = httpHelper.PostAsyncStringBody(pmisInfo?.Url + "/unioa/job/weekWork/insertDailyCommunication", workWeekBody.ToString(Formatting.None),
                                    new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } })
                                .Result;
                            result = JsonSerializer.Deserialize<PMISInsertResponse>(postRespone.Content.ReadAsStringAsync().Result);
                            if (result.Success) pushMessageHelper.Push("周报", $"第{weekDayInfo.WeekNumber}周周报已发送\n本周完成" + finishCount + " 条任务", PushMessageHelper.PushIcon.Note);
                            else pushMessageHelper.Push("周报错误:", postRespone.Content.ReadAsStringAsync().Result, PushMessageHelper.PushIcon.Alert);
                            return result;
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception e)
        {
            pushMessageHelper.Push("周报异常:", e.Message, PushMessageHelper.PushIcon.Alert);
            logger.LogError("周报异常:" + e.Message);
            return result;
        }
    }

    /// <summary>
    /// 获取当前是第几周，以及周一和周日的日期
    /// </summary>
    /// <returns></returns>
    public WeekDayInfo GetWeekDayInfo()
    {
        var currentDate = DateTime.Now;
        var ci = new CultureInfo("zh-CN"); // 使用中国文化，可以根据需求修改
        var weekNumber = ci.Calendar.GetWeekOfYear(currentDate, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        var startOfWeek = currentDate.AddDays(-(int)currentDate.DayOfWeek + (int)DayOfWeek.Monday);
        var endOfWeek = startOfWeek.AddDays(6);
        var info = new WeekDayInfo();
        info.WeekNumber = weekNumber;
        info.StartOfWeek = startOfWeek.ToString("yyyy-MM-dd");
        info.EndOfWeek = endOfWeek.ToString("yyyy-MM-dd");
        return info;
    }
}
using System.Data;
using System.Globalization;
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
        var postResponse = httpHelper.PostAsync(pmisInfo!.Url + "/unioa/job/userWork/queryMy", new
        {
            index = 1,
            size = 30,
            conditions = new object[] { },
            order = new object[] { },
            data = new
            {
                status = (object)null!,
                hasFile = (object)null!,
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
        var getResponse = httpHelper.GetAsync(pmisInfo!.Url + $"/unioa/job/userWork/getByDateAndUserId?fillDate={fillDate}&userId={userId}&type=0",
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
            if (projectJson["Response"]?["rows"] is JArray { Count: > 0 } dataArray)
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
            { "work_type", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "3" : "1" },
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
            { "product_name", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "GIS管网地理系统" : "" } // 明确声明为 null
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
            { "work_type", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "3" : "1" },
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
            { "product_name", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "GIS管网地理系统" : "" }, // null 明确声明
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
            { "work_overtime_type", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "3" : "1" },
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
            { "product_name", string.IsNullOrEmpty(projectInfo.contract_id) && string.IsNullOrEmpty(projectInfo.contract_unit) ? "GIS管网地理系统" : "" }
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

    /// <summary>
    /// 提交实际加班申请，并返回获取实际待审核加班列表
    /// </summary>
    /// <returns></returns>
    public JArray RealOverTimeList()
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var projectCode = string.Empty;
        var fieldMap = new JObject
        {
            ["_next_assignee"] = "下一步处理人员",
            ["$$countersign"] = "会签人员",
            ["id"] = "主键id",
            ["user_sn"] = "申请人工号",
            ["pms_pushed"] = "pms推送状态 0 未推送 1 推送成功 -1 推送失败",
            ["work_date"] = "加班日期",
            ["plan_start_time"] = "计划加班开始时间",
            ["plan_end_time"] = "计划加班结束时间",
            ["start_time"] = "考勤加班开始时间",
            ["end_time"] = "考勤加班结束时间",
            ["work_overtime_hour"] = "加班时长（小时）",
            ["work_overtime_hour_sub"] = "减小时数（小时）",
            ["realtime"] = "最终时长（小时",
            ["real_subject_matter"] = "实际加班内容",
            ["plan_is_pass"] = "是否通过",
            ["plan_approval_opinion"] = "审批意见",
            ["approval_user_id"] = "审批人id",
            ["approval_user_sn"] = "审批人工号",
            ["user_id"] = "申请人",
            ["org_id"] = "所属部门",
            ["work_overtime_type"] = "加班类型",
            ["work_type"] = "工作类型",
            ["contract_id"] = "合同编号",
            ["contract_unit"] = "合同单位",
            ["project_name"] = "项目名称",
            ["product_name"] = "产品名称",
            ["position"] = "加班地点",
            ["plan_work_overtime_hour"] = "计划加班时长（小时）",
            ["subject_matter"] = "加班事由",
            ["reason"] = "加班原因",
            ["order_no"] = "任务单号",
            ["remark"] = "备注"
        };
        var chatOptions = new ChatOptions { Tools = [] };
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        //获取所有待处理的实际加班列表
        var postResponse = httpHelper.PostAsync(
            pmisInfo.Url + $@"/bpm/customize-api/task/query?uniwater_utoken={tokenService.GetTokenAsync()}&HDSN={pmisInfo.UserAccount}&orderState=todo&user={pmisInfo.UserId}&index=1&size=30", new
            {
            }, new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } }).Result;
        var jsonArr = JArray.Parse(postResponse.Content.ReadAsStringAsync().Result);
        var results = new JArray();
        foreach (var item in jsonArr)
        {
            var obj = item as JObject;
            if (obj == null) continue;
            if (string.IsNullOrEmpty(obj["instance"]?["id"]?.ToString()) || string.IsNullOrEmpty(obj["task"]?["id"]?.ToString())) continue;
            var result = new JObject
            {
                ["_instance"] = obj["instance"]
            };
            //将variables展开铺平
            var variables = obj["variables"] as JObject;
            if (variables != null)
                foreach (var prop in variables.Properties())
                    result[prop.Name] = prop.Value?["value"] ?? JValue.CreateNull();

            if (!string.IsNullOrEmpty(result["id"]?.ToString()))
                projectCode = dbConnection.Query<string>($@"select contract_id  from public.overtimerecord where orderid = '{result["id"]?.ToString()}';").FirstOrDefault();

            //获取当前加班申请的实际工时，如果实际加班时长不足1小时，则跳出处理
            var overTimeResponse = httpHelper.PostAsync(
                pmisInfo.Url + $@"/hd-oa/api/oaWorkOvertime/getWorkOvertimeData", new
                {
                    workDate = result["work_date"]?.ToString(),
                    userId = pmisInfo.UserId
                }, new Dictionary<string, string> { { "uniwater_utoken", tokenService.GetTokenAsync() } }).Result;
            var overTimeResult = JObject.Parse(overTimeResponse.Content.ReadAsStringAsync().Result);
            if (!bool.Parse(overTimeResult["success"]?.ToString() ?? string.Empty)) continue;
            if (string.IsNullOrEmpty(overTimeResult["data"]?["startTime"]?.ToString()) || string.IsNullOrEmpty(overTimeResult["data"]?["endTime"]?.ToString())) continue;
            var overTimeHours = Math.Floor((DateTime.Parse(overTimeResult["data"]?["endTime"]?.ToString() ?? string.Empty) -
                                            DateTime.Parse(overTimeResult["data"]?["startTime"]?.ToString() ?? string.Empty)).TotalHours * 2) / 2;
            if (overTimeHours < 1)
            {
                //不是今天的单子，并且没有加班时长，则作废申请单
                if (result["work_date"]?.ToString() != DateTime.Now.ToString("yyyy-MM-dd"))
                {
                    var intervalDays = (DateTime.Now - DateTime.Parse(result["work_date"]?.ToString())).Days;
                    if (intervalDays > 2)
                    {
                        var cancelResponse = httpHelper.PostAsync(
                            pmisInfo.Url + $@"/bpm/customize-api/{obj["task"]?["id"]?.ToString()}/cancel", new
                            {
                                complete_id = pmisInfo.UserId,
                                complete_nm = pmisInfo.UserName,
                                isRecover = 0,
                                message = "1",
                                type = "error"
                            }, new Dictionary<string, string> { { "uniwaterutoken", tokenService.GetTokenAsync() } }).Result;
                        var cancelResult = JObject.Parse(cancelResponse.Content.ReadAsStringAsync().Result);
                        if (int.Parse(cancelResult["Code"]?.ToString() ?? string.Empty) != 0 || cancelResult["Message"]?.ToString().ToLower() != "ok") continue;
                        dbConnection.Execute($@"update
                                        	public.overtimerecord
                                        set
                                        	real_start_time = '{overTimeResult["data"]?["startTime"]?.ToString()}',
                                        	real_end_time = '{overTimeResult["data"]?["endTime"]?.ToString()}',
                                        	real_work_overtime_hour = {overTimeHours}
                                        where
                                        	work_date = '{result["work_date"]?.ToString()}';");
                        pushMessageHelper.Push("实际加班", result["work_date"]?.ToString() + " 加班时长不足1小时已作废\n", PushMessageHelper.PushIcon.OverTime);
                    }
                }
            }
            else
            {
                //获取实际加班下一步处理人相关信息
                var realApplyResponse = httpHelper.PostAsync(
                    pmisInfo.Url + $@"/hddev/form/formobjectdata/oa_work_overtime_real_apply:13/detail.json", new
                    {
                        id = result["id"]?.ToString()
                    }, new Dictionary<string, string> { { "uniwater_utoken", tokenService.GetTokenAsync() } }).Result;
                var realApplyResult = JObject.Parse(realApplyResponse.Content.ReadAsStringAsync().Result);
                if (int.Parse(realApplyResult["Code"]?.ToString() ?? string.Empty) != 0 || realApplyResult["Message"]?.ToString().ToLower() != "ok") continue;
                //获取$$formHtmlId参数信息
                var historyResponse = httpHelper.GetAsync(
                    pmisInfo.Url + $@"/bpm/customize-api/instance/{obj["instance"]?["id"]?.ToString()}/history?formHtmlId={result["$$formHtmlId"]?.ToString()}&noSubformField=1",
                    new Dictionary<string, string> { { "uniwater_utoken", tokenService.GetTokenAsync() } }).Result;
                if (!historyResponse.IsSuccessStatusCode) continue;
                var historyjsonArray = JArray.Parse(historyResponse.Content.ReadAsStringAsync().Result);
                var targetTaskId = realApplyResult["Response"]?["hddev_proc_task_code"]?.ToString();
                string outFormId = null;
                foreach (var historyitem in historyjsonArray)
                {
                    var taskId = historyitem["task"]?["id"]?.ToString();
                    if (!string.IsNullOrEmpty(taskId) && taskId.Contains(targetTaskId))
                    {
                        var initFormPropertyStr = historyitem["extensionProperties"]?["initFormProperty"]?.ToString();
                        if (!string.IsNullOrEmpty(initFormPropertyStr))
                            try
                            {
                                var initFormJson = JObject.Parse(initFormPropertyStr);
                                outFormId = initFormJson["outFormId"]?.ToString();
                            }
                            catch (Exception ex)
                            {
                                continue;
                            }

                        break;
                    }
                }

                //提交实际加班参数补全
                result["$$formHtmlId"] = outFormId;
                result["$$assignee"] = realApplyResult["Response"]?["approval_user_id"]?.ToString();
                result["$$assignee_nm"] = realApplyResult["Response"]?["approval_user_id$$text"]?.ToString();
                result["hddev_proc_task"] = realApplyResult["Response"]?["hddev_proc_task"]?.ToString();
                result["hddev_proc_task_code"] = realApplyResult["Response"]?["hddev_proc_task_code"]?.ToString();
                result["complete_id"] = pmisInfo.UserId;
                result["complete_sn"] = pmisInfo.UserAccount;
                result["complete_mobile"] = pmisInfo.UserMobile;
                result["complete_nm"] = pmisInfo.UserName;
                result["_automatic"] = JValue.CreateNull();
                result["$$fieldMap"] = fieldMap;

                //deepseek润色工作总结
                var chatHistory = new List<ChatMessage>
                {
                    new(ChatRole.System, "帮我根据计划加班事由生成一个实际加班理由，尽量不脱离原有语义进行重写，不要出现任何表述(实际加班理由：)，直接输出结果，字数控制在100字以内"),
                    new(ChatRole.User, "计划加班事由:" + result["subject_matter"])
                };
                var deepSeekRes = chatClient.GetResponseAsync(chatHistory, chatOptions).Result;
                var deepSeekContent = deepSeekRes.Text;
                if (string.IsNullOrEmpty(deepSeekContent)) continue;

                result["_next_assignee"] = realApplyResult["Response"]?["approval_user_id"]?.ToString();
                result["$$countersign"] = JValue.CreateNull();
                result["approval_opinion"] = JValue.CreateNull();
                result["is_production"] = JValue.CreateNull();
                result["work_overtime_hour_sub"] = 0;
                result["real_subject_matter"] = deepSeekContent;
                result["is_pass"] = JValue.CreateNull();
                result["realtime"] = overTimeHours;
                result["child_groups_name"] = JValue.CreateNull();
                result["end_time"] = overTimeResult["data"]?["endTime"]?.ToString();
                result["pms_pushed_result"] = JValue.CreateNull();
                result["product_name"] = string.IsNullOrEmpty(projectCode) ? "GIS管网地理系统" : "";
                result["work_overtime_hour"] = overTimeHours;
                result["start_time"] = overTimeResult["data"]?["startTime"]?.ToString();
                result["hddev_business_key"] = result["business_key"];
                result["start_time$$text"] = overTimeResult["data"]?["startTime"]?.ToString();
                result["end_time$$text"] = overTimeResult["data"]?["endTime"]?.ToString();

                var nextDeptTaskLimitResponse = httpHelper.GetAsync(
                    pmisInfo.Url + $@"/bpm/customize-api/task/{obj["task"]?["id"]?.ToString()}/getNextDeptTaskLimit?deptId=67", null,
                    new Dictionary<string, string> { { "uniwaterutoken", tokenService.GetTokenAsync() } }).Result;
                var nextDeptTaskLimitResult = JObject.Parse(nextDeptTaskLimitResponse.Content.ReadAsStringAsync().Result);
                if (int.Parse(nextDeptTaskLimitResult["Code"].ToString()) != 0 || nextDeptTaskLimitResult["Message"].ToString().ToLower() != "ok") continue;
                var suspendedResponse = httpHelper.PostAsync(
                    pmisInfo.Url + $@"/bpm/customize-api/{obj["instance"]?["id"]?.ToString()}/suspended?taskId=", new
                    {
                        suspended = false,
                        message = "",
                        filePath = "",
                        tagId = "",
                        comment = false,
                        type = "",
                        nextAssignee = "",
                        bpmSuspendedReminder = (object)null
                    },
                    new Dictionary<string, string> { { "uniwaterutoken", tokenService.GetTokenAsync() } }).Result;
                var suspendedResult = JObject.Parse(suspendedResponse.Content.ReadAsStringAsync().Result);
                if (int.Parse(suspendedResult["Code"].ToString()) != 0 || suspendedResult["Message"].ToString().ToLower() != "ok") continue;

                var completeResponse = httpHelper.PostAsyncStringBody(
                    pmisInfo.Url + $@"/bpm/customize-api/{obj["task"]?["id"]?.ToString()}/complete", result.ToString(Formatting.Indented),
                    new Dictionary<string, string> { { "uniwaterutoken", tokenService.GetTokenAsync() } }).Result;
                var completeResult = JObject.Parse(completeResponse.Content.ReadAsStringAsync().Result);
                if (int.Parse(completeResult["Code"].ToString()) != 0 || completeResult["Message"].ToString().ToLower() != "ok") continue;
                dbConnection.Execute($@"update
                                        	public.overtimerecord
                                        set
                                        	real_start_time = '{overTimeResult["data"]?["startTime"]?.ToString()}',
                                        	real_end_time = '{overTimeResult["data"]?["endTime"]?.ToString()}',
                                        	real_work_overtime_hour = {overTimeHours}
                                        where
                                        	work_date = '{result["work_date"]?.ToString()}';");
                pushMessageHelper.Push("实际加班", result["work_date"]?.ToString() + " 实际加班申请已提交\n加班时长：" + overTimeHours + " 小时", PushMessageHelper.PushIcon.OverTime);
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// 根据日期获取考勤数量
    /// </summary>
    /// <param name="clockInDate"></param>
    /// <returns></returns>
    public int GetTodayClockInDetail(string clockInDate)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var getResponse = httpHelper.GetAsync(pmisInfo.Url + $"/app/hd-oa/api/oaUserClockInRecord/todayClockInDetail?clockInDate={clockInDate}&detailStatus=1",
            new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() }, { "appid", pmisInfo.AppId }, { "app", pmisInfo.App } }).Result;
        var json = JObject.Parse(getResponse.Content.ReadAsStringAsync().Result);
        // 安全地取出 detailList
        var detailList = json["data"]?["detailList"] as JArray;
        var count = 0;
        if (detailList == null) return count;
        foreach (var item in detailList) count = detailList.Count;

        return count;
    }
}
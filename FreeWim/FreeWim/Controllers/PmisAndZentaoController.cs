using System.ComponentModel;
using System.Data;
using Dapper;
using FreeWim.Attributes;
using FreeWim.Services;
using FreeWim.Utils;
using FreeWim.Models.PmisAndZentao;
using FreeWim.Models.PmisAndZentao.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Npgsql;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class PmisAndZentaoController(
    IConfiguration configuration,
    ZentaoService zentaoService,
    AttendanceService attendanceService,
    PmisService pmisService,
    PushMessageService pushMessageService,
    TokenService tokenService,
    IChatClient chatClient,
    IWebHostEnvironment webHostEnvironment)
    : Controller
{
    [Tags("禅道")]
    [EndpointSummary("获取禅道Token(有效期24分钟)")]
    [HttpGet]
    [McpExposed("get_zentao_token", "获取禅道Token", "zentao")]
    public string GetZentaoToken()
    {
        return zentaoService.GetZentaoToken();
    }

    [Tags("禅道")]
    [EndpointSummary("获取我的任务列表")]
    [HttpGet]
    [McpExposed("get_zentao_tasks", "获取禅道任务列表", "zentao")]
    public string GetMyWorkTask()
    {
        return zentaoService.GetZentaoTask().ToString();
    }

    [Tags("禅道")]
    [EndpointSummary("同步禅道任务")]
    [HttpGet]
    [McpExposed("sync_zentao_tasks", "同步禅道任务到本地", "zentao")]
    public bool GetZentaoTask()
    {
        return zentaoService.SynchronizationZentaoTask();
    }

    [Tags("禅道")]
    [EndpointSummary("完成任务")]
    [HttpGet]
    [McpExposed("finish_task", "完成禅道任务", "zentao")]
    public string FinishTask([Description("任务完成日期")] DateTime finishedDate, [Description("完成工时数")] double totalHours)
    {
        zentaoService.FinishZentaoTask(finishedDate, totalHours);
        return "成功";
    }

    [Tags("禅道")]
    [EndpointSummary("计算工时")]
    [HttpGet]
    [McpExposed("allocate_work", "计算工时分配", "zentao")]
    public List<TaskItem> AllocateWork([Description("开始日期")] DateTime startDate, [Description("总工时数")] double totalHours)
    {
        var result = zentaoService.AllocateWork(startDate, totalHours);
        return result;
    }

    [Tags("禅道")]
    [EndpointSummary("根据项目ID获取项目编码")]
    [HttpGet]
    [McpExposed("get_project_code", "根据项目ID获取项目编码", "zentao")]
    public string GetProjectCodeForProjectId([Description("禅道项目ID")] string projectId)
    {
        var result = zentaoService.GetProjectCodeForProjectId(projectId);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("获取PMIS管理员token")]
    [HttpGet]
    [McpExposed("get_pmis_admin_token", "获取PMIS管理员token", "pmis")]
    public IActionResult GetPmisAdminToken()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var result = tokenService.GetAdminTokenAsync();
        return Ok(new { token = result, url = pmisInfo.Url });
    }

    [Tags("PMIS")]
    [EndpointSummary("根据日期计算工时")]
    [HttpGet]
    [McpExposed("get_work_hours", "根据日期计算工时", "pmis")]
    public double GetWorkHoursByDate([Description("查询日期，一般默认当前时间")] DateTime date)
    {
        var result = attendanceService.GetWorkHoursByDate(date);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("获取已上报列表")]
    [HttpGet]
    [McpExposed("query_daily_reports", "查询PMIS日报列表", "pmis")]
    public string QueryMyByDate()
    {
        var json = pmisService.QueryMyByDate();
        return json.ToString(Newtonsoft.Json.Formatting.None);
    }

    [Tags("PMIS")]
    [EndpointSummary("获取工作明细")]
    [HttpGet]
    [McpExposed("query_work_details", "获取工作明细", "pmis")]
    public string QueryByDateAndUserId([Description("查询日期，一般默认当前时间，格式为：yyyy-MM-dd")] string fillDate = "2025-06-27")
    {
        var attempt = 0;
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var result = pmisService.QueryWorkDetailByDate(fillDate, pmisInfo.UserId);
        while (int.Parse(result["Code"]?.ToString() ?? "0") != 0 ||
               !bool.Parse(result["Success"]?.ToString() ?? "false"))
            try
            {
                if (attempt >= 20)
                {
                    pushMessageService.Push("提交日报异常", "多次尝试获取今日工作内容失败", PushMessageService.PushIcon.Alert);
                    break;
                }

                attempt++;
                result = pmisService.QueryWorkDetailByDate(fillDate, pmisInfo.UserId);
            }
            catch (Exception)
            {
                // ignored
            }

        return result.ToString(Newtonsoft.Json.Formatting.None);
    }

    [Tags("PMIS")]
    [EndpointSummary("提交工作日志")]
    [HttpGet]
    [McpExposed("submit_work_log", "提交工作日志", "pmis")]
    public PMISInsertResponse CommitWorkLogByDate([Description("填报日期，格式为：yyyy-MM-dd")] string fillDate = "2025-06-27")
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var result = pmisService.CommitWorkLogByDate(fillDate, pmisInfo.UserId);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("测试推送")]
    [HttpGet]
    [McpExposed("push_message", "推送消息", "pmis")]
    public string PushMessage([Description("消息标题")] string Title = "禅道", [Description("消息内容")] string message = "测试消息")
    {
        pushMessageService.Push(Title, message + DateTime.Now.ToString(), PushMessageService.PushIcon.Zentao);
        return "";
    }

    [Tags("PMIS")]
    [EndpointSummary("获取项目信息")]
    [HttpGet]
    [McpExposed("get_project_info", "获取项目信息", "pmis")]
    public ProjectInfo GetProjectInfo([Description("项目编码")] string projectCode)
    {
        var result = pmisService.GetProjectInfo(projectCode);
        return result;
    }


    [Tags("DeepSeek")]
    [EndpointSummary("生成加班理由")]
    [HttpGet]
    [McpExposed("generate_overtime_content", "生成加班理由", "deepseek")]
    public string GeneratedOvertimeWorkContent([Description("加班内容描述")] string Content)
    {
        try
        {
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
            var chatOptions = new ChatOptions
            {
                Tools =
                [
                ]
            };
            var chatHistory = new List<ChatMessage>
            {
                new(ChatRole.System, pmisInfo.DailyPrompt),
                new(ChatRole.User, "加班内容：" + Content)
            };
            var res = chatClient.GetResponseAsync(chatHistory, chatOptions).Result;
            var json = res.Text;
            return json;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    [Tags("PMIS")]
    [EndpointSummary("提交加班申请")]
    [HttpGet]
    [McpExposed("submit_overtime", "提交加班申请", "pmis")]
    public string SubmitOvertime()
    {
        try
        {
            var projectInfo = new ProjectInfo();
            var workStart = new TimeSpan(13, 30, 0); // 13:30
            var workEnd = new TimeSpan(20, 30, 0); // 20:30
            if (DateTime.Now.TimeOfDay < workStart || DateTime.Now.TimeOfDay > workEnd) return "";
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            //判断休息日不提交加班
            var checkinrule = dbConnection
                .Query<string>(
                    $@"select checkinrule from public.attendancerecordday where to_char(attendancedate,'yyyy-MM-dd')  = to_char(now(),'yyyy-MM-dd')")
                .FirstOrDefault();
            if (checkinrule == "休息") return "";
            //查询是否打卡上班
            var clockinCount = dbConnection
                .Query<int>(
                    $@"SELECT COUNT(0) FROM public.attendancerecorddaydetail WHERE clockintype= '0' AND TO_CHAR(clockintime,'yyyy-MM-dd') = to_char(now(),'yyyy-MM-dd')")
                .FirstOrDefault();
            if (clockinCount == 0) return "";
            //查询是否已提交加班申请
            var hasOvertime = dbConnection
                .Query<int>(
                    $@"select count(0) from  public.overtimerecord where work_date = '{DateTime.Now:yyyy-MM-dd}'")
                .FirstOrDefault();
            if (hasOvertime != 0) return "";
            var zentaoInfo = dbConnection.Query<dynamic>($@"select
                                                                            id,
                                                                        	project,
	                                                                        taskname ,
	                                                                        taskdesc,
	                                                                        projectcode
                                                                        from
                                                                        	zentaotask z
                                                                        where
                                                                        	to_char(eststarted,
                                                                        	'yyyy-MM-dd') = to_char(now(),
                                                                        	'yyyy-MM-dd')
                                                                        	and taskstatus = 'wait'
                                                                        order by
                                                                        	timeleft desc").FirstOrDefault();

            if (zentaoInfo?.project == null || zentaoInfo?.id == null ||
                string.IsNullOrEmpty(zentaoInfo?.projectcode)) return "";
            if (string.IsNullOrEmpty(zentaoInfo?.projectcode)) return "";
            if (zentaoInfo?.projectcode == "GIS-Product")
                projectInfo = new ProjectInfo
                {
                    contract_id = "",
                    contract_unit = "",
                    project_name = "GIS外业管理系统"
                };
            else
                projectInfo = pmisService.GetProjectInfo(zentaoInfo?.projectcode);

            if (string.IsNullOrEmpty(projectInfo.project_name)) return "";
            // var chatOptions = new ChatOptions { Tools = [] };
            // var chatHistory = new List<ChatMessage>
            // {
            //     new(ChatRole.System, pmisInfo.DailyPrompt),
            //     new(ChatRole.User, "加班内容：" + zentaoInfo.taskname + ":" + zentaoInfo.taskdesc)
            // };
            // var res = chatClient.GetResponseAsync(chatHistory, chatOptions).Result;
            // if (string.IsNullOrWhiteSpace(res?.Text)) return "";
            // var workContent = res.Text;
            // if (string.IsNullOrEmpty(workContent)) return "";
            // var insertId = pmisHelper.OvertimeWork_Insert(projectInfo, zentaoInfo?.id.ToString(), workContent);
            // if (string.IsNullOrEmpty(insertId)) return "";
            // var processId = pmisHelper.OvertimeWork_CreateOrder(projectInfo, insertId, zentaoInfo?.id.ToString(), workContent);
            // if (!string.IsNullOrEmpty(processId))
            // {
            //     JObject updateResult = pmisHelper.OvertimeWork_Update(projectInfo, insertId, zentaoInfo?.id.ToString(), processId, workContent);
            //     if (updateResult["Response"] != null)
            //     {
            //         pushMessageHelper.Push("加班申请", DateTime.Now.ToString("yyyy-MM-dd") + " 加班申请已提交\n加班事由：" + workContent, PushMessageHelper.PushIcon.OverTime);
            //         dbConnection.Execute($@"
            //                           insert
            //                           	into
            //                           	public.overtimerecord
            //                           (id,
            //                           	plan_start_time,
            //                           	plan_end_time,
            //                           	plan_work_overtime_hour,
            //                           	contract_id,
            //                           	contract_unit,
            //                           	project_name,
            //                           	work_date,
            //                           	subject_matter,
            //                           	orderid)
            //                           values('{Guid.NewGuid().ToString()}',
            //                           '{updateResult["Response"]?["plan_start_time"]}',
            //                           '{updateResult["Response"]?["plan_end_time"]}',
            //                           {updateResult["Response"]?["plan_work_overtime_hour"]},
            //                           '{updateResult["Response"]?["contract_id"]}',
            //                           '{updateResult["Response"]?["contract_unit"]}',
            //                           '{updateResult["Response"]?["project_name"]}',
            //                           '{updateResult["Response"]?["work_date"]}',
            //                           '{updateResult["Response"]?["subject_matter"]}',
            //                           '{updateResult["Response"]?["id"]}');");
            //     }
            // }
        }
        catch (Exception e)
        {
            pushMessageService.Push("加班申请异常", e.Message, PushMessageService.PushIcon.Alert);
        }

        return "";
    }

    [Tags("PMIS")]
    [EndpointSummary("获取本周是第几周以及周一到周日的日期")]
    [HttpGet]
    [McpExposed("get_week_info", "获取周信息", "pmis")]
    public string GetWeekDayInfo()
    {
        var weekInfo = pmisService.GetWeekDayInfo();
        return $"当前日期是本年的第 {weekInfo.WeekNumber.ToString().PadLeft(2, '0')} 周;周一：" + weekInfo.StartOfWeek + ";周日:" +
               weekInfo.EndOfWeek;
    }

    [Tags("PMIS")]
    [EndpointSummary("周报上报")]
    [HttpGet]
    [McpExposed("submit_week_work", "提交周报", "pmis")]
    public PMISInsertResponse GetWeekWork()
    {
        var result = pmisService.CommitWorkLogByWeek(pmisService.GetWeekDayInfo());
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("PMIS组件数据查询接口")]
    [HttpGet]
    [McpExposed("query_latest_data", "查询最新数据", "pmis")]
    public ActionResult latest()
    {
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var taskCount = dbConnection
            .Query<int>(
                @"select count(0) as taskcount from zentaotask z where to_char(z.eststarted,'yyyy-MM-dd')  = to_char(now(),'yyyy-MM-dd')")
            .First();
        var overtime = dbConnection
            .Query<string>(
                @"select case when count(0) > 1 then '已提交' else '未提交' end as overtimere from overtimerecord z where z.work_date  = to_char(now(),'yyyy-MM-dd')")
            .First();

        return Json(new
        {
            taskCount,
            overtime
            //DayAvg = Math.Round(uniqueCommits / (double)commitDates, 2)
        });
    }

    [Tags("PMIS")]
    [EndpointSummary("提交所有待处理实际加班申请")]
    [HttpGet]
    [McpExposed("query_real_overtime_list", "查询实际加班列表", "pmis")]
    public string RealOverTimeList()
    {
        return pmisService.RealOverTimeList().ToString(Newtonsoft.Json.Formatting.Indented);
    }

    [Tags("PMIS")]
    [EndpointSummary("根据日期获取考勤数量")]
    [HttpGet]
    [McpExposed("get_today_clock_in_detail", "获取今日打卡详情", "pmis")]
    public int GetTodayClockInDetail([Description("打卡日期，格式为：yyyy-MM-dd")] string clockInDate)
    {
        return pmisService.GetTodayClockInDetail(clockInDate);
    }

    /// <summary>
    /// 获取实际加班信息
    /// </summary>
    /// <param name="startTime">开始时间，格式：yyyy-MM-dd HH:mm:ss</param>
    /// <param name="endTime">结束时间，格式：yyyy-MM-dd HH:mm:ss</param>
    /// <returns>加班信息列表</returns>
    [Tags("PMIS")]
    [EndpointSummary("获取实际加班信息")]
    [HttpGet]
    [McpExposed("get_overtime_records", "获取实际加班信息", "pmis")]
    public List<OaWorkoverTimeOutput> GetOaWorkoverTime(
        [Description("查询开始日期，一般默认为每月26日如：2025-01-26")] string startTime = "",
        [Description("查询结束日期，一般默认为每月25日如：2025-02-25")] string endTime = "")
    {
        var result = pmisService.GetOaWorkoverTime(startTime, endTime);
        return result;
    }

    /// <summary>
    /// 导出餐补记录
    /// </summary>
    /// <returns></returns>
    [Tags("PMIS")]
    [EndpointSummary("导出餐补记录")]
    [HttpPost]
    [McpExposed("export_overtime_records", "导出加班餐补记录", "pmis")]
    public ActionResult ExportOaWorkoverTime(
        [Description("查询开始日期，一般默认为每月26日如：2025-01-26")]
        string startTime = "",
        [Description("查询结束日期，一般默认为每月25日如：2025-02-25")]
        string endTime = "")
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;

        // 取加班数据
        var result = pmisService.GetOaWorkoverTime(startTime, endTime);

        // 构建 DataTable 和列配置
        var dataTable = new DataTable();
        var columnList = new List<ExcelHelper.ExportDataColumn>();

        // 姓名列
        dataTable.Columns.Add("Name", typeof(string));
        columnList.Add(new ExcelHelper.ExportDataColumn
        {
            Prop = "Name",
            Label = "姓名",
            ColumnWidth = 256 * 20,
            Type = ""
        });

        // 获取唯一日期，并按时间排序（防止乱序）
        var uniqueDates = result
            .Select(r => DateTime.Parse(r.Attendancedate ?? string.Empty))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        // 动态列
        foreach (var dt in uniqueDates)
        {
            var colName = $"{dt:MM.dd}开发实施加班";

            dataTable.Columns.Add(colName, typeof(double));

            columnList.Add(new ExcelHelper.ExportDataColumn
            {
                Prop = colName,
                Label = colName,
                ColumnWidth = 256 * 10,
                Type = ""
            });
        }

        // 构建数据行
        var row = dataTable.NewRow();
        row["Name"] = pmisInfo.UserName;

        // 通过字典提升速度 & 去掉重复 FirstOrDefault
        var dateValueMap = result
            .GroupBy(e => DateTime.Parse(e.Attendancedate ?? string.Empty).ToString("MM.dd"))
            .ToDictionary(
                g => $"{g.Key}开发实施加班",
                g => g.First().Amount
            );

        foreach (DataColumn col in dataTable.Columns)
            if (dateValueMap.TryGetValue(col.ColumnName, out var amount))
                row[col.ColumnName] = amount;

        dataTable.Rows.Add(row);

        // 输出路径
        var path = Path.Combine(webHostEnvironment.ContentRootPath, "Export", Guid.NewGuid().ToString());

        // 生成 Excel 文件
        var fileName = ExcelHelper.ExportForCommonNoTitle(dataTable, columnList, path);
        var filePath = Path.Combine(path, fileName);

        // 返回文件
        var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(fs, "application/vnd.ms-excel", fileName);
    }

    /// <summary>
    /// 获取PMIS日报汇总
    /// </summary>
    /// <returns></returns>
    [Tags("PMIS")]
    [EndpointSummary("获取PMIS日报汇总")]
    [HttpPost]
    [McpExposed("query_job_user_work_sum", "查询日报汇总", "pmis")]
    public IResult QueryJobUserWorkSum([Description("查询条件")] QueryJobUserWorkSumInput input)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var targetUrl = pmisInfo.Url.TrimEnd('/') + "/unioa/job/userWorkSum/queryJobUserWorkSum";
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(targetUrl, new
            {
                index = 1,
                size = -1,
                conditions = Array.Empty<object>(),
                order = Array.Empty<object>(),
                data = new
                {
                    systemId = (string?)null,
                    groupId = (string?)null,
                    classId = (string?)null,
                    time = new[] { input.StartDate, input.EndDate },
                    restDay = false,
                    beginDate = input.StartDate,
                    endDate = input.EndDate
                }
            },
            new Dictionary<string, string> { { "authorization", tokenService.GetAdminTokenAsync() ?? string.Empty } })
            .Result;

        var result = postResponse.Content.ReadAsStringAsync().Result;
        return Results.Content(result, "application/json");
    }

    /// <summary>
    /// 获取PMIS日报明细
    /// </summary>
    /// <returns></returns>
    [Tags("PMIS")]
    [EndpointSummary("获取PMIS日报明细")]
    [HttpPost]
    [McpExposed("query_user_work_sum_detail", "查询日报明细", "pmis")]
    public IResult QueryUserWorkSumDetail([Description("查询条件")] QueryUserWorkSumDetailInput input)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var targetUrl = pmisInfo.Url.TrimEnd('/') + "/unioa/job/userWorkSum/queryUserWorkSumDetail";
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(targetUrl, new
            {
                index = 1,
                size = -1,
                conditions = Array.Empty<object>(),
                order = Array.Empty<object>(),
                data = new
                {
                    systemId = (string?)null,
                    groupId = (string?)null,
                    classId = (string?)null,
                    createDate = input.CreateDate
                }
            },
            new Dictionary<string, string> { { "authorization", tokenService.GetAdminTokenAsync() ?? string.Empty } })
            .Result;

        var result = postResponse.Content.ReadAsStringAsync().Result;
        return Results.Content(result, "application/json");
    }

    /// <summary>
    /// 获取PMIS周报汇总
    /// </summary>
    /// <returns></returns>
    [Tags("PMIS")]
    [EndpointSummary("获取PMIS周报汇总")]
    [HttpPost]
    [McpExposed("query_job_user_work_week_sum", "查询周报汇总", "pmis")]
    public IResult QueryJobUserWorkWeekSum([Description("查询条件")] QueryJobUserWorkSumInput input)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var targetUrl = pmisInfo.Url.TrimEnd('/') + "/unioa/job/userWorkWeekSum/queryJobUserWorkWeekSum";
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(targetUrl, new
            {
                index = 1,
                size = -1,
                conditions = Array.Empty<object>(),
                order = Array.Empty<object>(),
                data = new
                {
                    systemId = (string?)null,
                    groupId = (string?)null,
                    classId = (string?)null,

                    time = new[] { input.StartDate, input.EndDate },
                    restDay = false,
                    beginDate = input.StartDate,
                    endDate = input.EndDate
                }
            },
            new Dictionary<string, string> { { "authorization", tokenService.GetAdminTokenAsync() ?? string.Empty } })
            .Result;

        var result = postResponse.Content.ReadAsStringAsync().Result;
        return Results.Content(result, "application/json");
    }

    /// <summary>
    /// 获取PMIS周报明细
    /// </summary>
    /// <returns></returns>
    [Tags("PMIS")]
    [EndpointSummary("获取PMIS周报明细")]
    [HttpPost]
    [McpExposed("query_user_work_week_sum_detail", "查询周报明细", "pmis")]
    public IResult QueryUserWorkWeekSumDetail([Description("查询条件")] queryUserWorkWeekSumDetailInput input)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var targetUrl = pmisInfo.Url.TrimEnd('/') + "/unioa/job/userWorkWeekSum/queryUserWorkWeekSumDetail";
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(targetUrl, new
            {
                index = 1,
                size = -1,
                conditions = Array.Empty<object>(),
                order = Array.Empty<object>(),
                data = new
                {
                    systemId = (string?)null,
                    groupId = (string?)null,
                    classId = (string?)null,
                    weekStart = input.WeekStart
                }
            },
            new Dictionary<string, string> { { "authorization", tokenService.GetAdminTokenAsync() ?? string.Empty } })
            .Result;

        var result = postResponse.Content.ReadAsStringAsync().Result;
        return Results.Content(result, "application/json");
    }

    /// <summary>
    /// 获取一诺部门使用情况
    /// </summary>
    /// <returns></returns>
    [Tags("PMIS")]
    [EndpointSummary("获取一诺部门使用情况")]
    [HttpPost]
    [McpExposed("query_org_page", "查询部门使用情况", "pmis")]
    public IResult OrgPage([Description("查询条件")] OrgPageInput input)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var targetUrl = pmisInfo.Url.TrimEnd('/') + "/uniwim/message/useSta/orgPage";
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(targetUrl, new
            {
                index = 1,
                size = -1,
                data = new
                {
                    endTime = input.EndTime,
                    startTime = input.StartTime,
                    orgIds = input.OrgIds
                }
            },
            new Dictionary<string, string> { { "authorization", tokenService.GetAdminTokenAsync() ?? string.Empty } })
            .Result;
        var result = postResponse.Content.ReadAsStringAsync().Result;
        return Results.Content(result, "application/json");
    }

    /// <summary>
    /// 获取一诺人员使用情况
    /// </summary>
    /// <returns></returns>
    [Tags("PMIS")]
    [EndpointSummary("获取一诺人员使用情况")]
    [HttpPost]
    [McpExposed("query_person_page", "查询人员使用情况", "pmis")]
    public IResult PersonPage([Description("查询条件")] OrgPageInput input)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var targetUrl = pmisInfo.Url.TrimEnd('/') + "/uniwim/message/useSta/personPage";
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(targetUrl, new
            {
                index = 1,
                size = -1,
                data = new
                {
                    endTime = input.EndTime,
                    startTime = input.StartTime,
                    orgIds = input.OrgIds
                }
            },
            new Dictionary<string, string> { { "authorization", tokenService.GetAdminTokenAsync() ?? string.Empty } })
            .Result;
        var result = postResponse.Content.ReadAsStringAsync().Result;
        return Results.Content(result, "application/json");
    }
}
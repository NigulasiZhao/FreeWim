using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Npgsql;
using FreeWim.Common;
using FreeWim.Models.PmisAndZentao;
using Newtonsoft.Json.Linq;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class PmisAndZentaoController(
    IConfiguration configuration,
    ILogger<SpeedTestController> logger,
    ZentaoHelper zentaoHelper,
    AttendanceHelper attendanceHelper,
    PmisHelper pmisHelper,
    PushMessageHelper pushMessageHelper,
    TokenService tokenService,
    IChatClient chatClient)
    : Controller
{
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<SpeedTestController> _logger = logger;

    [Tags("禅道")]
    [EndpointSummary("获取禅道Token(有效期24分钟)")]
    [HttpGet]
    public string GetZentaoToken()
    {
        return zentaoHelper.GetZentaoToken();
    }

    [Tags("禅道")]
    [EndpointSummary("获取我的任务列表")]
    [HttpGet]
    public string GetMyWorkTask()
    {
        return zentaoHelper.GetZentaoTask().ToString();
    }

    [Tags("禅道")]
    [EndpointSummary("同步禅道任务")]
    [HttpGet]
    public bool GetZentaoTask()
    {
        return zentaoHelper.SynchronizationZentaoTask();
    }

    [Tags("禅道")]
    [EndpointSummary("完成任务")]
    [HttpGet]
    public string FinishTask(DateTime finishedDate, double totalHours)
    {
        zentaoHelper.FinishZentaoTask(finishedDate, totalHours);
        return "成功";
    }

    [Tags("禅道")]
    [EndpointSummary("计算工时")]
    [HttpGet]
    public List<TaskItem> AllocateWork(DateTime startDate, double totalHours)
    {
        var result = zentaoHelper.AllocateWork(startDate, totalHours);
        return result;
    }

    [Tags("禅道")]
    [EndpointSummary("根据项目ID获取项目编码")]
    [HttpGet]
    public string GetProjectCodeForProjectId(string projectId)
    {
        var result = zentaoHelper.GetProjectCodeForProjectId(projectId);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("获取PMIS管理员token")]
    [HttpGet]
    public IActionResult GetPmisAdminToken()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var result = tokenService.GetAdminTokenAsync();
        return Ok(new { token = result, url = pmisInfo.Url });
    }

    [Tags("PMIS")]
    [EndpointSummary("根据日期计算工时")]
    [HttpGet]
    public double GetWorkHoursByDate(DateTime date)
    {
        var result = attendanceHelper.GetWorkHoursByDate(date);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("获取已上报列表")]
    [HttpGet]
    public string QueryMyByDate()
    {
        var json = pmisHelper.QueryMyByDate();
        return json.ToString(Newtonsoft.Json.Formatting.None);
    }

    [Tags("PMIS")]
    [EndpointSummary("获取工作明细")]
    [HttpGet]
    public string QueryByDateAndUserId(string fillDate = "2025-06-27")
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var result = pmisHelper.QueryWorkDetailByDate(fillDate, pmisInfo.UserId);
        return result.ToString(Newtonsoft.Json.Formatting.None);
    }

    [Tags("PMIS")]
    [EndpointSummary("提交工作日志")]
    [HttpGet]
    public PMISInsertResponse CommitWorkLogByDate(string fillDate = "2025-06-27")
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var result = pmisHelper.CommitWorkLogByDate(fillDate, pmisInfo.UserId);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("测试推送")]
    [HttpGet]
    public string PushMessage(string Title = "禅道", string message = "测试消息")
    {
        pushMessageHelper.Push(Title, message + DateTime.Now.ToString(), PushMessageHelper.PushIcon.Zentao);
        return "";
    }

    [Tags("PMIS")]
    [EndpointSummary("通过项目编码获取PMIS项目信息")]
    [HttpGet]
    public ProjectInfo GetProjectInfo(string projectCode)
    {
        var result = pmisHelper.GetProjectInfo(projectCode);
        return result;
    }


    [Tags("DeepSeek")]
    [EndpointSummary("生成加班理由")]
    [HttpGet]
    public string GeneratedOvertimeWorkContent(string Content)
    {
        try
        {
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
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
    public string SubmitOvertime()
    {
        try
        {
            var projectInfo = new ProjectInfo();
            var workStart = new TimeSpan(13, 30, 0); // 13:30
            var workEnd = new TimeSpan(20, 30, 0); // 20:30
            if (DateTime.Now.TimeOfDay < workStart || DateTime.Now.TimeOfDay > workEnd) return "";
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            //判断休息日不提交加班
            var checkinrule = dbConnection.Query<string>($@"select checkinrule from public.attendancerecordday where to_char(attendancedate,'yyyy-MM-dd')  = to_char(now(),'yyyy-MM-dd')")
                .FirstOrDefault();
            if (checkinrule == "休息") return "";
            //查询是否打卡上班
            var clockinCount = dbConnection
                .Query<int>($@"SELECT COUNT(0) FROM public.attendancerecorddaydetail WHERE clockintype= '0' AND TO_CHAR(clockintime,'yyyy-MM-dd') = to_char(now(),'yyyy-MM-dd')")
                .FirstOrDefault();
            if (clockinCount == 0) return "";
            //查询是否已提交加班申请
            var hasOvertime = dbConnection.Query<int>($@"select count(0) from  public.overtimerecord where work_date = '{DateTime.Now:yyyy-MM-dd}'").FirstOrDefault();
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

            if (zentaoInfo?.project == null || zentaoInfo?.id == null || string.IsNullOrEmpty(zentaoInfo?.projectcode)) return "";
            if (string.IsNullOrEmpty(zentaoInfo?.projectcode)) return "";
            if (zentaoInfo?.projectcode == "GIS-Product")
                projectInfo = new ProjectInfo
                {
                    contract_id = "",
                    contract_unit = "",
                    project_name = "GIS外业管理系统"
                };
            else
                projectInfo = pmisHelper.GetProjectInfo(zentaoInfo?.projectcode);

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
            pushMessageHelper.Push("加班申请异常", e.Message, PushMessageHelper.PushIcon.Alert);
        }

        return "";
    }

    [Tags("PMIS")]
    [EndpointSummary("获取本周是第几周以及周一到周日的日期")]
    [HttpGet]
    public string GetWeekDayInfo()
    {
        var weekInfo = pmisHelper.GetWeekDayInfo();
        return $"当前日期是本年的第 {weekInfo.WeekNumber} 周;周一：" + weekInfo.StartOfWeek + ";周日:" + weekInfo.EndOfWeek;
    }

    [Tags("PMIS")]
    [EndpointSummary("周报上报")]
    [HttpGet]
    public PMISInsertResponse GetWeekWork()
    {
        var result = pmisHelper.CommitWorkLogByWeek(pmisHelper.GetWeekDayInfo());
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("PMIS组件数据查询接口")]
    [HttpGet]
    public ActionResult latest()
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var taskCount = dbConnection.Query<int>(@"select count(0) as taskcount from zentaotask z where to_char(z.eststarted,'yyyy-MM-dd')  = to_char(now(),'yyyy-MM-dd')").First();
        var overtime = dbConnection.Query<string>(@"select case when count(0) > 1 then '已提交' else '未提交' end as overtimere from overtimerecord z where z.work_date  = to_char(now(),'yyyy-MM-dd')")
            .First();

        return Json(new
        {
            taskCount,
            overtime
            //DayAvg = Math.Round(uniqueCommits / (double)commitDates, 2)
        });
    }

    [Tags("PMIS")]
    [EndpointSummary("获取实际加班待处理列表")]
    [HttpGet]
    public string RealOverTimeList()
    {
        return pmisHelper.RealOverTimeList().ToString(Newtonsoft.Json.Formatting.Indented);
    }

    [Tags("PMIS")]
    [EndpointSummary("根据日期获取考勤数量")]
    [HttpGet]
    public int GetTodayClockInDetail(string clockInDate)
    {
        return pmisHelper.GetTodayClockInDetail(clockInDate);
    }
}
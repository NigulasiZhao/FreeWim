using System.Data;
using Dapper;
using FreeWim.Models;
using FreeWim.Models.Attendance;
using FreeWim.Models.Attendance.Dto;
using FreeWim.Models.PmisAndZentao;
using FreeWim.Services;
using FreeWim.Utils;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Npgsql;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AttendanceRecordController(
    IConfiguration configuration,
    AttendanceService attendanceService,
    PushMessageService
        pushMessageService) : Controller
{
    /// <summary>
    /// 日期打卡数据查询
    /// </summary>
    /// <param name="date"></param>
    /// <param name="sn"></param>
    /// <returns></returns>
    [Tags("考勤")]
    [EndpointSummary("日期打卡数据查询")]
    [HttpGet]
    public async Task<ActionResult> PunchCardQuery(string date, string sn)
    {
        var records = await attendanceService.GetPunchCardRecordsFromExternalApi(date, sn);

        // Group by pin and select the earliest check-in and latest check-out
        var result = records.GroupBy(r => r.Pin)
            .Select(g =>
            {
                var ename = g.First().Ename;
                var checkTimes = g.Where(r => DateTime.TryParse(r.Checktime, out _))
                    .Select(r => DateTime.Parse(r.Checktime!))
                    .OrderBy(dt => dt)
                    .ToList();

                DateTime? startTime = null;
                DateTime? endTime = null;

                if (checkTimes.Count >= 2)
                {
                    startTime = checkTimes.First();
                    endTime = checkTimes.Last();
                }
                else if (checkTimes.Count == 1)
                {
                    // If only one record, treat it as start time
                    startTime = checkTimes.First();
                }

                return new PunchCardQueryOutput
                {
                    Ename = ename ?? string.Empty,
                    StartTime = startTime,
                    EndTime = endTime
                };
            })
            .ToList();

        return Json(new
        {
            ret = 0,
            msg = $"获取考勤记录 {result.Count} 条。",
            data = new
            {
                count = result.Count,
                items = result
            }
        });
    }

    [Tags("考勤")]
    [EndpointSummary("考勤组件数据查询接口")]
    [HttpGet]
    public ActionResult latest()
    {
        var result = attendanceService.GetLatestAttendanceStats();
        return Json(result);
    }

    [Tags("考勤")]
    [EndpointSummary("日历数据")]
    [HttpGet]
    public ActionResult calendar(string start = "", string end = "")
    {
        var workList = attendanceService.GetCalendarData(start, end);
        return Json(workList);
    }

    [Tags("考勤")]
    [EndpointSummary("取消加班")]
    [HttpPost]
    public ActionResult CancelOverTimeWork()
    {
        var rowsCount = attendanceService.CancelOverTimeWork();
        return Json(new { rowsCount });
    }

    [Tags("考勤")]
    [EndpointSummary("恢复自动加班")]
    [HttpPost]
    public ActionResult RestoreOverTimeWork()
    {
        var result = attendanceService.RestoreOverTimeWork();
        return Json(result);
    }

    [Tags("考勤")]
    [EndpointSummary("获取考勤面板数据")]
    [HttpGet]
    public ActionResult GetBoardData()
    {
        var result = attendanceService.GetBoardData();
        return Json(result);
    }

    [Tags("自动打卡")]
    [EndpointSummary("创建自动打卡计划")]
    [HttpPost]
    public ActionResult AutoCheckIn([FromBody] AutoCheckInInput input)
    {
        var result = attendanceService.CreateAutoCheckIn(input);
        return Json(result);
    }

    [Tags("自动打卡")]
    [EndpointSummary("取消自动打卡计划")]
    [HttpPost]
    public ActionResult CancelAutoCheckIn([FromBody] AutoCheckInInput input)
    {
        var flag = attendanceService.CancelAutoCheckIn(input);
        return Json(new { flag });
    }

    [Tags("自动打卡")]
    [EndpointSummary("获取自动打卡计划列表")]
    [HttpGet]
    public ActionResult GetAutoCheckInList(int page = 1, int rows = 10)
    {
        var result = attendanceService.GetAutoCheckInList(page, rows);
        return Json(result);
    }

    [Tags("考勤")]
    [EndpointSummary("获取周末加班数据")]
    [HttpPost]
    public ActionResult WorkingOvertimeOnWeekends(WorkingOvertimeOnWeekendsInput input)
    {
        var result = attendanceService.GetWorkingOvertimeOnWeekends(input);
        return Json(result);
    }

    [Tags("考勤")]
    [EndpointSummary("工时排名")]
    [HttpPost]
    public ActionResult RankingOfWorkingHours(RankingOfWorkingHoursInput input)
    {
        var result = attendanceService.GetRankingOfWorkingHours(input);
        return Json(result);
    }

    [Tags("考勤")]
    [EndpointSummary("高级工时统计")]
    [HttpPost]
    public ActionResult AdvancedWorkHoursStatistics(RankingOfWorkingHoursInput input)
    {
        var result = attendanceService.GetAdvancedWorkHoursStatistics(input);
        return Json(result);
    }

    [Tags("考勤")]
    [EndpointSummary("考勤异常汇总")]
    [HttpPost]
    public ActionResult AttendanceAbnormalSummary(AttendanceAbnormalSummaryInput input)
    {
        var result = attendanceService.GetAttendanceAbnormalSummary(input);
        return Json(result);
    }

    [Tags("考勤")]
    [EndpointSummary("考勤异常明细")]
    [HttpPost]
    public ActionResult AttendanceAbnormalDetail(AttendanceAbnormalDetailInput input)
    {
        var result = attendanceService.GetAttendanceAbnormalDetail(input);
        return Json(result);
    }

    [Tags("考勤")]
    [EndpointSummary("公司范围动作触发(0进入，1离开)")]
    [HttpPost]
    public async Task<ActionResult> RangeAction([FromBody] RangeActionInput input)
    {
        if (input.Type == 0) return await HandleEnterRange();
        if (input.Type == 1) return await HandleLeaveRange();
        return Json(new { success = false, message = "参数错误" });
    }

    [Tags("考勤")]
    [EndpointSummary("测试-公司范围动作触发(0进入，1离开)")]
    [HttpPost]
    public ActionResult RangeActionTest([FromBody] RangeActionInput input)
    {
        if (input.Type == 0)
        {
            pushMessageService.Push("测试提醒", "Type" + input.Type, PushMessageService.PushIcon.Windows);
            return Json(new { success = true, message = "设备已开启", isWorking = 1 });
        }

        if (input.Type == 1)
        {
            pushMessageService.Push("测试提醒", "Type" + input.Type, PushMessageService.PushIcon.Windows);
            return Json(new { success = true, message = "设备已开启", isWorking = 1 });
        }

        return Json(new { success = true, message = "设备已开启", isWorking = 1 });
    }

    private async Task<ActionResult> HandleEnterRange()
    {
        try
        {
            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);

            // 执行查询判断是否在工作
            var sql = @"SELECT 
                CASE 
                    WHEN (
                        a.checkinrule != '休息' 
                        OR EXISTS (
                            SELECT 1 
                            FROM public.overtimerecord o 
                            WHERE o.plan_start_time::date = CURRENT_DATE
                        )
                    ) 
                    AND NOT EXISTS (
                        SELECT 1 
                        FROM public.attendancerecorddaydetail d 
                        WHERE d.clockintime::date = CURRENT_DATE
                    ) 
                    THEN 1
                    ELSE 0 
                END AS isworking
            FROM 
                public.attendancerecordday a
            WHERE 
                a.attendancedate::date = CURRENT_DATE
            LIMIT 1;";

            var isWorking = dbConnection.Query<int>(sql).FirstOrDefault();

            if (isWorking == 1)
            {
                var homeAssistantInfo = configuration.GetSection("HomeAssistant").Get<HomeAssistantInfo>();

                if (homeAssistantInfo == null || string.IsNullOrEmpty(homeAssistantInfo.Url))
                {
                    return Json(new { success = false, message = "HomeAssistant配置未找到" });
                }

                var httpHelper = new HttpRequestHelper();
                var response = await httpHelper.PostAsync(homeAssistantInfo.Url, new
                    {
                        entity_id = homeAssistantInfo.EntityId
                    },
                    new Dictionary<string, string>
                        { { "Authorization", homeAssistantInfo.Authorization ?? string.Empty } });

                if (response.IsSuccessStatusCode)
                {
                    pushMessageService.Push("开机提醒", "已为您开启电脑", PushMessageService.PushIcon.Windows);
                    return Json(new { success = true, message = "设备已开启", isWorking = 1 });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Json(new
                        { success = false, message = $"调用Home Assistant失败: {errorContent}", isWorking = 1 });
                }
            }
            else
            {
                return Json(new { success = true, message = "不在工作状态，无需控制设备", isWorking = 0 });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"执行失败: {ex.Message}" });
        }
    }

    private async Task<ActionResult> HandleLeaveRange()
    {
        try
        {
            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var httpRequestHelper = new HttpRequestHelper();
            // 1. 判断是否存在未执行的自动打卡记录
            var existautocheckin = dbConnection.Query<int>(
                    $"SELECT COUNT(0) FROM public.autocheckinrecord WHERE to_char(clockintime,'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}' and clockinstate = 0 ")
                .First();
            if (existautocheckin > 0)
            {
                return Json(new { success = true });
            }

            // 2. 获取今日工时
            var workHours = dbConnection.Query<double>(
                    "SELECT workhours FROM public.attendancerecordday WHERE attendancedate::date = CURRENT_DATE LIMIT 1")
                .FirstOrDefault();

            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;

            if (workHours > 0)
            {
                // 3. 工时大于0，调用关机接口
                if (!string.IsNullOrEmpty(pmisInfo.ShutDownUrl))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var uri = new Uri(pmisInfo.ShutDownUrl);
                            using var tcpClient = new System.Net.Sockets.TcpClient();
                            var connectTask = tcpClient.ConnectAsync(uri.Host, uri.Port);
                            if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask)
                            {
                                await connectTask;
                                if (tcpClient.Connected)
                                {
                                    pushMessageService.Push("关机提醒", "您的电脑即将关机", PushMessageService.PushIcon.Close);
                                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                                    await client.GetAsync(pmisInfo.ShutDownUrl);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 忽略关机接口调用失败，可能是机器已关机
                            Console.WriteLine($"调用关机接口失败: {ex.Message}");
                        }
                    });
                }

                return Json(new { success = true });
            }
            else
            {
                var response = await httpRequestHelper.PostAsync(
                    pmisInfo.ZkUrl + "/api/v2/transaction/get/?key=" + pmisInfo.ZkKey,
                    new
                    {
                        starttime = DateTime.Now.ToString("yyyy-MM-dd 00:00:00"),
                        endtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });

                var result = await response.Content.ReadAsStringAsync();
                var resultModel = JsonConvert.DeserializeObject<ZktResponse>(result);
                var pin = "100" + pmisInfo.UserAccount;
                var myRecords = resultModel?.Data?.Items?
                    .Where(e => e.Pin == pin && !string.IsNullOrEmpty(e.Checktime))
                    .Select(e => DateTime.Parse(e.Checktime!))
                    .OrderBy(t => t)
                    .ToList();

                var totalHours = 0.0;
                if (myRecords is { Count: >= 2 })
                {
                    for (int i = 1; i < myRecords.Count; i++)
                    {
                        totalHours += (myRecords[i] - myRecords[i - 1]).TotalHours;
                    }
                }

                if (myRecords is { Count: >= 2 } && totalHours >= 1)
                {
                    if (!string.IsNullOrEmpty(pmisInfo.ShutDownUrl))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var uri = new Uri(pmisInfo.ShutDownUrl);
                                using var tcpClient = new System.Net.Sockets.TcpClient();
                                var connectTask = tcpClient.ConnectAsync(uri.Host, uri.Port);
                                if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask)
                                {
                                    await connectTask;
                                    if (tcpClient.Connected)
                                    {
                                        pushMessageService.Push("关机提醒", "您的电脑即将关机,已为您触发考勤同步",
                                            PushMessageService.PushIcon.Close);
                                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                                        await client.GetAsync(pmisInfo.ShutDownUrl);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // 忽略关机接口调用失败，可能是机器已关机
                                Console.WriteLine($"调用关机接口失败: {ex.Message}");
                            }
                        });
                    }

                    // 5. 本人打卡数据大于等于两条，触发同步和关机
                    attendanceService.SyncAttendanceRecord();
                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = true });
                }
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"执行失败: {ex.Message}" });
        }
    }

    [Tags("考勤")]
    [EndpointSummary("用户打卡明细")]
    [HttpPost]
    public ActionResult GetUserClockInDetails(UserClockInDetailsInput input)
    {
        var result = attendanceService.GetUserClockInDetails(input);
        return Json(result);
    }
}
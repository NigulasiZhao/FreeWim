using System.Data;
using Dapper;
using FreeWim.Models.Attendance;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using FreeWim.Models.PmisAndZentao;

namespace FreeWim.Services;

public class WorkFlowExecutorService(
    IConfiguration configuration,
    PushMessageService pushMessageService,
    IServiceProvider serviceProvider,
    PmisService pmisService,
    ZentaoService zentaoService)
{
    public void ExecuteAll()
    {
        try
        {
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var existautocheckin =
                dbConnection.Query<int>($"SELECT COUNT(0) FROM public.autocheckinrecord WHERE to_char(clockintime,'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}' and clockinstate = 0 ").First();
            if (existautocheckin > 0) return;

            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
            var attendanceService = serviceProvider.GetRequiredService<AttendanceService>();
            var workHours = attendanceService.GetWorkHoursByDate(DateTime.Today);
            if (workHours > 0)
            {
                var reportList = pmisService.QueryMyByDate();
                if (bool.Parse((string)reportList["Success"]!))
                    if (reportList["Response"]!["rows"] is JArray dataArray)
                    {
                        var record = dataArray.FirstOrDefault(item => item["fillDate"]?.ToString() == DateTime.Now.ToString("yyyy-MM-dd"));
                        if (workHours > 0 && record == null)
                        {
                            //关闭禅道任务
                            zentaoService.FinishZentaoTask(DateTime.Today, workHours);
                            //生成日志
                            var taskFinishInfo = dbConnection.Query<TaskFinishInfo>($@"SELECT
                                        COUNT(CASE WHEN taskstatus = 'done' THEN 1 END) AS donecount,
                                        COUNT(CASE WHEN taskstatus != 'done' THEN 1 END) AS notdonecount,
                                        count(0) as allcount
                                    FROM public.zentaotask
                                    WHERE to_char(eststarted, 'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}'").First();
                            if (taskFinishInfo.AllCount > 0 && taskFinishInfo is { NotDoneCount: 0, DoneCount: > 0 })
                                pmisService.CommitWorkLogByDate(DateTime.Now.ToString("yyyy-MM-dd"), pmisInfo.UserId);
                        }
                    }
            }

            var workStart = new TimeSpan(17, 30, 0); // 16:30
            if (DateTime.Now.TimeOfDay < workStart) return;
            //验证是否发周报
            var weekInfo = pmisService.GetWeekDayInfo();
            var weekSummary = dbConnection.Query<WeekSummaryDto>($@"select
	                                                        to_char(MAX(attendancedate),'yyyy-MM-dd') AS lastday,
                                                            sum(workhours) AS weekhours
                                                        from
                                                            public.attendancerecordday
                                                        where
                                                            attendancedate >= '{weekInfo.StartOfWeek} 00:00:00'
                                                            and attendancedate <= '{weekInfo.EndOfWeek} 23:59:59'
                                                            and checkinrule = '08:30-17:30' ").FirstOrDefault();
            if (weekSummary == null || string.IsNullOrEmpty(weekSummary.LastDay)) return;
            if (weekSummary.LastDay != DateTime.Now.ToString("yyyy-MM-dd")) return;

            if (weekSummary.WeekHours > 0)
                pmisService.CommitWorkLogByWeek(weekInfo);
        }
        catch (Exception e)
        {
            pushMessageService.Push("提交日报异常", e.Message, PushMessageService.PushIcon.Alert);
        }
    }
}
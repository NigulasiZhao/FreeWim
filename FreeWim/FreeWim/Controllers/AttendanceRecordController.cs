using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using FreeWim.Models;
using FreeWim.Models.Attendance;
using System.Data;
using System.Globalization;
using Dapper;
using FreeWim.Common;
using FreeWim.Models.Attendance.Dto;
using FreeWim.Models.PmisAndZentao;
using Hangfire;
using Npgsql;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AttendanceRecordController(IConfiguration configuration, AttendanceHelper attendanceHelper) : Controller
{
    [Tags("考勤")]
    [EndpointSummary("考勤组件数据查询接口")]
    [HttpGet]
    public ActionResult latest()
    {
        IDbConnection _DbConnection = new NpgsqlConnection(configuration["Connection"]);
        var WorkDays = _DbConnection
            .Query<int>("select count(0) from (select to_char(attendancedate,'yyyy-mm-dd'),count(0) from public.attendancerecorddaydetail  group by to_char(attendancedate,'yyyy-mm-dd'))").First();
        var WorkHours = _DbConnection.Query<decimal>("select sum(workhours) from public.attendancerecordday").First();
        _DbConnection.Dispose();
        return Json(new
        {
            WorkDays = WorkDays,
            WorkHours = WorkHours,
            DayAvg = Math.Round((double)WorkHours / WorkDays, 2)
        });
    }

    [Tags("考勤")]
    [EndpointSummary("日历数据")]
    [HttpGet]
    public ActionResult calendar(string start = "", string end = "")
    {
        IDbConnection _DbConnection = new NpgsqlConnection(configuration["Connection"]);
        var sqlwhere = " where 1=1 ";
        if (!string.IsNullOrEmpty(start)) sqlwhere += $" and a.clockintime >= '{DateTime.Parse(start)}'";
        if (!string.IsNullOrEmpty(end)) sqlwhere += $" and a.clockintime <= '{DateTime.Parse(end).AddDays(1).AddSeconds(-1)}'";
        var WorkList = _DbConnection.Query<AttendanceCalendarOutput>(@"select
                                                                                                    	a.id as rownum,
                                                                                                    	case
                                                                                                    		a.clockintype when '0' then '上班'
                                                                                                    		else '下班'
                                                                                                    	end as title,
                                                                                                    	to_char(timezone('UTC',
                                                                                                    	a.clockintime at TIME zone 'Asia/Shanghai'),
                                                                                                    	'yyyy-mm-ddThh24:mi:ssZ') as airDateUtc,
                                                                                                    	true as hasFile,
                                                                                                    	case
                                                                                                    		when b.workhours = 0 then 
                                                                                                    		'当日工时: ' || RTRIM(RTRIM(cast(ROUND(extract(EPOCH
                                                                                                    	from
                                                                                                    		(now() at TIME zone 'Asia/Shanghai' - a.clockintime))/ 3600,
                                                                                                    		1) as VARCHAR),
                                                                                                    		'0'),
                                                                                                    		'.')|| ' 小时'
                                                                                                    		else
                                                                                                    	'当日工时: ' || RTRIM(RTRIM(cast(b.workhours as VARCHAR),
                                                                                                    		'0'),
                                                                                                    		'.') || ' 小时'
                                                                                                    	end as workhours
                                                                                                    from
                                                                                                    	public.attendancerecorddaydetail a
                                                                                                    left join attendancerecordday b on
                                                                                                    	to_char(a.attendancedate,
                                                                                                    	'yyyy-mm-dd') = to_char(b.attendancedate,
                                                                                                    	'yyyy-mm-dd')
                                                                                                    " + sqlwhere + " order by clockintime").ToList();
        _DbConnection.Dispose();
        return Json(WorkList);
    }

    [Tags("考勤")]
    [EndpointSummary("取消加班")]
    [HttpPost]
    public ActionResult CancelOverTimeWork([FromBody] CancelOverTimeWorkInput input)
    {
        var rowsCount = 0;
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var cancelCount = dbConnection.Query<int>($@"SELECT COUNT(0) FROM public.overtimerecord WHERE work_date = '{DateTime.Now:yyyy-MM-dd}';").FirstOrDefault();
        if (cancelCount == 0)
            rowsCount = dbConnection.Execute($@"insert
														into
														public.overtimerecord
													(id,
														work_date,contract_unit)
													values('{Guid.NewGuid()}', '{DateTime.Now:yyyy-MM-dd}','');");
        dbConnection.Dispose();
        return Json(new { rowsCount });
    }

    [Tags("考勤")]
    [EndpointSummary("恢复自动加班")]
    [HttpPost]
    public ActionResult RestoreOverTimeWork([FromBody] CancelOverTimeWorkInput input)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var rowsCount = dbConnection.Execute($@"DELETE FROM 
														public.overtimerecord
													WHERE work_date = '{DateTime.Now:yyyy-MM-dd}';");
        dbConnection.Dispose();
        return Json(new
        {
            rowsCount, pmisInfo.OverStartTime, pmisInfo.OverEndTime,
            TotalHours = (DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime) - DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime))
                .TotalHours
                .ToString() + "h"
        });
    }

    [Tags("考勤")]
    [EndpointSummary("获取考勤面板数据")]
    [HttpGet]
    public ActionResult GetBoardData()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        IDbConnection _DbConnection = new NpgsqlConnection(configuration["Connection"]);
        var sqlThisMonth = @"
							        SELECT
  EXTRACT(DAY FROM attendancedate)::int AS day,
  SUM(workhours) OVER (ORDER BY attendancedate ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS workhours
FROM attendancerecordday
WHERE date_trunc('month', attendancedate) = date_trunc('month', now())
ORDER BY attendancedate";

        var sqlLastMonth = @"
							       SELECT
  EXTRACT(DAY FROM attendancedate)::int AS day,
  SUM(workhours) OVER (ORDER BY attendancedate ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS workhours
FROM attendancerecordday
WHERE date_trunc('month', attendancedate) = date_trunc('month', now() - interval '1 month')
ORDER BY attendancedate";

        var thisMonthData = _DbConnection.Query<(int Day, double Workhours)>(sqlThisMonth).ToList();
        var lastMonthData = _DbConnection.Query<(int Day, double Workhours)>(sqlLastMonth).ToList();

        var sql = @"
        SELECT
            TO_CHAR(attendancedate, 'YYYY-MM') AS Month,
            SUM(workhours) AS TotalHours,
            SUM(CASE when checkinrule <> '休息' AND workhours > 7.5 THEN workhours - 7.5 WHEN checkinrule = '休息' then workhours ELSE 0 END) AS OvertimeHours
        FROM attendancerecordday
        WHERE attendancedate >= date_trunc('month', CURRENT_DATE) - INTERVAL '5 months' and attendancedate < date_trunc('month', CURRENT_DATE) + INTERVAL '1 months'
        GROUP BY TO_CHAR(attendancedate, 'YYYY-MM')
        ORDER BY Month;
    ";

        var result = _DbConnection.Query<(string Month, double TotalHours, double OvertimeHours)>(sql);

        var sqlCheckIn = @"SELECT
    TO_CHAR(attendancedate::date, 'MM.DD') AS DayLabel,
    TO_CHAR(MIN(clockintime), 'HH24:MI') AS CheckInTime
FROM attendancerecorddaydetail
WHERE clockintype = '0'
  AND attendancedate >= CURRENT_DATE - INTERVAL '30 days'
  AND attendancedate < CURRENT_DATE + INTERVAL '1 day'
GROUP BY attendancedate::date
ORDER BY attendancedate::date

        ";

        var sqlCheckOut = @"
        SELECT
            TO_CHAR(attendancedate::date, 'MM.DD') AS DayLabel,
            TO_CHAR(MAX(clockintime), 'HH24:MI') AS CheckOutTime
        FROM attendancerecorddaydetail
        WHERE clockintype = '1'
          AND attendancedate >= CURRENT_DATE - INTERVAL '30 days'
  AND attendancedate < CURRENT_DATE + INTERVAL '1 day'
        GROUP BY attendancedate::date
        ORDER BY attendancedate::date;
        ";

        var checkIns = _DbConnection.Query<(string DayLabel, string CheckInTime)>(sqlCheckIn).ToList();
        var checkOuts = _DbConnection.Query<(string DayLabel, string CheckOutTime)>(sqlCheckOut).ToList();


        var punchHeatsql = @"
                        SELECT
                          TO_CHAR(attendancedate::date, 'YYYY-MM-DD') AS Date,
                          COALESCE(SUM(workhours), 0) AS Total
                        FROM attendancerecordday
                        WHERE attendancedate >= CURRENT_DATE - INTERVAL '1 year'
                        GROUP BY attendancedate::date
                        ORDER BY attendancedate::date
                        ";

        var punchHeatresult = _DbConnection.Query<(string Date, decimal Total)>(punchHeatsql);
        var punchHeatmap = punchHeatresult.Select(r => new object[] { r.Date, (double)r.Total }).ToList();

        var sqlforHeader = @"SELECT
    -- 本月总工时
    (SELECT SUM(workhours)
     FROM attendancerecordday
     WHERE yearmonth = to_char(current_date, 'YYYY-MM')) AS this_month,

    -- 上月总工时
    (SELECT SUM(workhours)
     FROM attendancerecordday
     WHERE yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM')) AS last_month,

    -- 上上月总工时
    (SELECT SUM(workhours)
     FROM attendancerecordday
     WHERE yearmonth = to_char(current_date - interval '2 month', 'YYYY-MM')) AS before_last_month,

    -- 本月 vs 上月 百分比变化
    CASE
        WHEN (SELECT SUM(workhours)
              FROM attendancerecordday
              WHERE yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM')) > 0
        THEN ROUND(
            (
                (SELECT SUM(workhours)
                 FROM attendancerecordday
                 WHERE yearmonth = to_char(current_date, 'YYYY-MM'))
                -
                (SELECT SUM(workhours)
                 FROM attendancerecordday
                 WHERE yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM'))
            ) / 
            (SELECT SUM(workhours)
             FROM attendancerecordday
             WHERE yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM')) * 100, 2)
        ELSE NULL
    END AS this_vs_last_percent,

    -- 上月 vs 上上月 百分比变化
    CASE
        WHEN (SELECT SUM(workhours)
              FROM attendancerecordday
              WHERE yearmonth = to_char(current_date - interval '2 month', 'YYYY-MM')) > 0
        THEN ROUND(
            (
                (SELECT SUM(workhours)
                 FROM attendancerecordday
                 WHERE yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM'))
                -
                (SELECT SUM(workhours)
                 FROM attendancerecordday
                 WHERE yearmonth = to_char(current_date - interval '2 month', 'YYYY-MM'))
            ) /
            (SELECT SUM(workhours)
             FROM attendancerecordday
             WHERE yearmonth = to_char(current_date - interval '2 month', 'YYYY-MM')) * 100, 2)
        ELSE NULL
    END AS last_vs_before_last_percent;
";
        var HeaderResult = _DbConnection.Query<(double thismonth, double lastmonth, double beforelastmonth, double thisvslastpercent, double lastvsbeforelastpercent)>(sqlforHeader);

        var sqlforavgworkhours = @"select
	round(this_avg.workhours / nullif(this_avg.days, 0), 2) as thisavghours,
	round(last_avg.workhours / nullif(last_avg.days, 0), 2) as lastavghours,
	ROUND(
        case 
            when last_avg.workhours is null or last_avg.days = 0 then null
            else 
                ((this_avg.workhours / nullif(this_avg.days, 0)) - (last_avg.workhours / nullif(last_avg.days, 0))) 
                / nullif((last_avg.workhours / last_avg.days), 0) * 100
        end, 
    2) as improve_percent
from
	(
	select
		SUM(workhours) as workhours,
		COUNT(distinct attendancedate::date) as days
	from
		attendancerecordday
	where
		yearmonth = to_char(current_date, 'YYYY-MM')
		and workhours > 0
) as this_avg,
	(
	select
		SUM(workhours) as workhours,
		COUNT(distinct attendancedate::date) as days
	from
		attendancerecordday
	where
		yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM')
			and workhours > 0
) as last_avg;";
        var avgworkhoursResult = _DbConnection.Query<(double thisavghours, double lastavghours, double improvepercent)>(sqlforavgworkhours);

        var sqlforavgovertime = @"select
	round(this_avg.overtimeday::numeric / nullif(this_avg.days, 0) * 100, 2) as thisavghours,
	round(last_avg.overtimeday::numeric / nullif(last_avg.days, 0) * 100, 2) as lastavghours,
	ROUND(
        case 
            when last_avg.overtimeday is null or last_avg.days = 0 then null
            else 
                ((this_avg.overtimeday::numeric / nullif(this_avg.days, 0)) - (last_avg.overtimeday::numeric / nullif(last_avg.days, 0))) 
                / nullif((last_avg.overtimeday::numeric / last_avg.days), 0) * 100
        end, 
    2) as improve_percent
from
	(
	select
		sum(case when workhours >= 8.5 then 1 else 0 end) as overtimeday,
		COUNT(distinct attendancedate::date) as days
	from
		attendancerecordday
	where
		yearmonth = to_char(current_date, 'YYYY-MM') and  to_char(attendancedate,'yyyy-MM-dd') <= to_char(now(),'yyyy-MM-dd') and checkinrule <> '休息'
) as this_avg,
	(
	select
		sum(case when workhours >= 8.5 then 1 else 0 end) as overtimeday,
		COUNT(distinct attendancedate::date) as days
	from
		attendancerecordday
	where
		yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM') and checkinrule <> '休息'
) as last_avg;";
        var avgovertimeResult = _DbConnection.Query<(double thisavghours, double lastavghours, double improvepercent)>(sqlforavgovertime);


        var sqlforovertimerecord = @"select
	id,
	work_date as date,
	contract_unit as contractunit,
	coalesce ( to_char(plan_start_time , 'HH24:MI'),
	'') as start,
	coalesce (to_char(plan_end_time , 'HH24:MI') ,
	'') as
end,
	coalesce ( plan_work_overtime_hour ,
0) as duration,
	case
	when plan_start_time is null then '未申请'
	else '已申请'
end as status
from
	public.overtimerecord
order by
work_date desc
limit 10;";
        var overtimerecord = _DbConnection.Query<(string id, string date, string contractunit, string start, string end, string duration, string status)>(sqlforovertimerecord).ToList();
        if (!overtimerecord.Exists(e => e.date == DateTime.Now.ToString("yyyy-MM-dd")))
        {
            overtimerecord.RemoveAt(overtimerecord.Count - 1);
            overtimerecord.Add(
                (
                    Guid.NewGuid().ToString(),
                    DateTime.Now.ToString("yyyy-MM-dd"),
                    "待定",
                    pmisInfo.OverStartTime,
                    pmisInfo.OverEndTime,
                    (DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime) - DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime)).TotalHours
                    .ToString(),
                    "待申请"
                ));
        }


        var endresult = new
        {
            header = new
            {
                thismonth = HeaderResult.FirstOrDefault().thismonth,
                lastmonth = HeaderResult.FirstOrDefault().lastmonth,
                beforelastmonth = HeaderResult.FirstOrDefault().beforelastmonth,
                thisvslastpercent = HeaderResult.FirstOrDefault().thisvslastpercent,
                lastvsbeforelastpercent = HeaderResult.FirstOrDefault().lastvsbeforelastpercent
            },
            avgWorkHours = new
            {
                thismonth = avgworkhoursResult.FirstOrDefault().thisavghours,
                lastmonth = avgworkhoursResult.FirstOrDefault().lastavghours,
                beforelastmonth = avgworkhoursResult.FirstOrDefault().improvepercent
            },
            avgOverTime = new
            {
                thismonth = avgovertimeResult.FirstOrDefault().thisavghours,
                lastmonth = avgovertimeResult.FirstOrDefault().lastavghours,
                beforelastmonth = avgovertimeResult.FirstOrDefault().improvepercent
            },
            overTimeRecord = overtimerecord.Select(e => new
            {
                id = e.id,
                date = e.date,
                contractunit = e.contractunit,
                start = e.start,
                end = e.end,
                duration = e.duration,
                status = e.status
            }).OrderByDescending(e => e.date).ToList(),
            monthTrend = new
            {
                labels = thisMonthData.Select(d => $"{d.Day}日").ToList(),
                thisMonth = thisMonthData.Select(d => d.Workhours).ToList(),
                lastMonth = lastMonthData.Select(d => d.Workhours).ToList()
            },
            monthBar = new
            {
                months = result.Select(r => $"{int.Parse(r.Month.Split('-')[1])}月").ToList(),
                totals = result.Select(r => r.TotalHours).ToList(),
                overtimes = result.Select(r => r.OvertimeHours).ToList()
            },
            checkIn = new
            {
                dates = checkIns.Select(c => c.DayLabel).ToList(),
                times = checkIns.Select(c => c.CheckInTime).ToList()
            },
            checkOut = new
            {
                dates = checkOuts.Select(c => c.DayLabel).ToList(),
                times = checkOuts.Select(c => c.CheckOutTime).ToList()
            },
            compare = new
            {
                thisMonth = 160,
                lastMonth = 150,
                lastYearMonth = 145,
                thisWeek = 40,
                lastWeek = 38
            },
            punchHeatmap = punchHeatmap
        };
        return Json(endresult);
    }

    [Tags("自动打卡")]
    [EndpointSummary("创建自动打卡计划")]
    [HttpPost]
    public ActionResult AutoCheckIn([FromBody] AutoCheckInInput input)
    {
        if (input.SelectTime != null)
        {
            if (input.SelectTime < DateTime.Now) return Json(new { jobId = "", SelectTime = input.SelectTime, message = "登记失败,时间不能小于当前时间" });
            //早上9点之前立即执行
            var workStart = new TimeSpan(10, 0, 0);
            if (input.SelectTime.Value.TimeOfDay > workStart)
            {
                var rand = new Random();
                var offsetSeconds = rand.Next(0, 500);
                input.SelectTime = input.SelectTime.Value.AddSeconds(offsetSeconds);
            }

            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var currentQuantity = dbConnection.Query<int>($@"SELECT count(0) FROM public.autocheckinrecord where to_char(clockintime,'yyyy-mm-dd') = '{input.SelectTime.Value:yyyy-MM-dd}'").First();
            if (currentQuantity >= 2) return Json(new { jobId = "", SelectTime = input.SelectTime, message = "登记失败,今日操作过于频繁" });
            var jobId = BackgroundJob.Schedule(() => attendanceHelper.AutoCheckIniclock(null), input.SelectTime.Value);
            dbConnection.Execute(
                $@"insert
                	into
                	public.autocheckinrecord(id, jobid, clockintime, clockinstate)
                values('{Guid.NewGuid().ToString()}', '{jobId}', to_timestamp('{input.SelectTime:yyyy-MM-dd HH:mm:ss}', 'yyyy-mm-dd hh24:mi:ss'), 0)");
            dbConnection.Dispose();
            return Json(new { jobId = jobId, SelectTime = input.SelectTime, message = "成功" });
        }
        else
        {
            return Json(new { jobId = "", SelectTime = input.SelectTime, message = "登记失败,请选择时间" });
        }
    }

    [Tags("自动打卡")]
    [EndpointSummary("取消自动打卡计划")]
    [HttpPost]
    public ActionResult CancelAutoCheckIn([FromBody] AutoCheckInInput input)
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var flag = BackgroundJob.Delete(input.jobId);
        dbConnection.Execute($@"DELETE FROM public.autocheckinrecord WHERE jobid = '{input.jobId}'");
        dbConnection.Dispose();
        return Json(new { flag });
    }

    [Tags("自动打卡")]
    [EndpointSummary("获取自动打卡计划列表")]
    [HttpGet]
    public ActionResult GetAutoCheckInList(int page = 1, int rows = 10)
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var offset = (page - 1) * rows;
        return Json(dbConnection.Query<AutoCheckInRecord>($@"SELECT * FROM public.autocheckinrecord ORDER BY clockintime desc LIMIT :rows OFFSET :offset", new { rows, offset }).ToList());
    }
}
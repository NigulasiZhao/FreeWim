using System.Data;
using System.Text;
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
public class AttendanceRecordController(IConfiguration configuration, AttendanceService attendanceService, PushMessageService
  pushMessageService) : Controller
{
    [Tags("考勤")]
    [EndpointSummary("考勤组件数据查询接口")]
    [HttpGet]
    public ActionResult latest()
    {
        using IDbConnection _DbConnection = new NpgsqlConnection(configuration["Connection"]);
        var WorkDays = _DbConnection
          .Query<int>(
            "select count(0) from (select to_char(attendancedate,'yyyy-mm-dd'),count(0) from public.attendancerecorddaydetail  group by to_char(attendancedate,'yyyy-mm-dd'))")
          .First();
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
        using IDbConnection _DbConnection = new NpgsqlConnection(configuration["Connection"]);
        var sqlwhere = " where 1=1 ";
        if (!string.IsNullOrEmpty(start)) sqlwhere += $" and a.clockintime >= '{DateTime.Parse(start)}'";
        if (!string.IsNullOrEmpty(end))
            sqlwhere += $" and a.clockintime <= '{DateTime.Parse(end).AddDays(1).AddSeconds(-1)}'";
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
                                                                                                    " + sqlwhere +
                                                                     " order by clockintime").ToList();
        _DbConnection.Dispose();
        return Json(WorkList);
    }

    [Tags("考勤")]
    [EndpointSummary("取消加班")]
    [HttpPost]
    public ActionResult CancelOverTimeWork([FromBody] CancelOverTimeWorkInput input)
    {
        var rowsCount = 0;
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var cancelCount = dbConnection
          .Query<int>($@"SELECT COUNT(0) FROM public.overtimerecord WHERE work_date = '{DateTime.Now:yyyy-MM-dd}';")
          .FirstOrDefault();
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
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var rowsCount = dbConnection.Execute($@"DELETE FROM 
														public.overtimerecord
													WHERE work_date = '{DateTime.Now:yyyy-MM-dd}';");
        dbConnection.Dispose();
        return Json(new
        {
            rowsCount,
            pmisInfo.OverStartTime,
            pmisInfo.OverEndTime,
            TotalHours = (DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime) -
                        DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime))
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
        using IDbConnection _DbConnection = new NpgsqlConnection(configuration["Connection"]);
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
        var HeaderResult =
          _DbConnection
            .Query<(double thismonth, double lastmonth, double beforelastmonth, double thisvslastpercent, double
              lastvsbeforelastpercent)>(sqlforHeader);

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
        var avgworkhoursResult =
          _DbConnection.Query<(double thisavghours, double lastavghours, double improvepercent)>(sqlforavgworkhours);

        // 重构后的加班率计算逻辑：加班天数 / 工作日天数 × 100%
        // 注意：排除今天的数据，因为当天工时可能还未完成
        var sqlforavgovertime = @"select
	-- 本月加班率：加班天数/工作日天数*100
	round(this_avg.overtimeday::numeric / nullif(this_avg.workdays, 0) * 100, 2) as thisavghours,
	-- 上月加班率：加班天数/工作日天数*100
	round(last_avg.overtimeday::numeric / nullif(last_avg.workdays, 0) * 100, 2) as lastavghours,
	-- 加班率变化百分比
	ROUND(
        case 
            when last_avg.overtimeday is null or last_avg.workdays = 0 then null
            else 
                ((this_avg.overtimeday::numeric / nullif(this_avg.workdays, 0)) - (last_avg.overtimeday::numeric / nullif(last_avg.workdays, 0))) 
                / nullif((last_avg.overtimeday::numeric / last_avg.workdays), 0) * 100
        end, 
    2) as improve_percent
from
	(
	select
		-- 本月加班天数（工时>=8.5小时的天数）
		sum(case when workhours >= 8.5 then 1 else 0 end) as overtimeday,
		-- 本月工作日天数（非休息日，排除今天）
		sum(case when checkinrule <> '休息' then 1 else 0 end) as workdays
	from
		attendancerecordday
	where
		yearmonth = to_char(current_date, 'YYYY-MM') 
		and to_char(attendancedate,'yyyy-MM-dd') < to_char(now(),'yyyy-MM-dd')
) as this_avg,
	(
	select
		-- 上月加班天数（工时>=8.5小时的天数）
		sum(case when workhours >= 8.5 then 1 else 0 end) as overtimeday,
		-- 上月工作日天数（非休息日）
		sum(case when checkinrule <> '休息' then 1 else 0 end) as workdays
	from
		attendancerecordday
	where
		yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM') 
) as last_avg;";
        var avgovertimeResult =
          _DbConnection.Query<(double thisavghours, double lastavghours, double improvepercent)>(sqlforavgovertime);


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
        var overtimerecord = _DbConnection
          .Query<(string id, string date, string contractunit, string start, string end, string duration, string status)>(
            sqlforovertimerecord).ToList();
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
                (DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime) -
                 DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime)).TotalHours
                .ToString(),
                "待申请"
              ));
        }


        var endresult = new
        {
            header = new
            {
                HeaderResult.FirstOrDefault().thismonth,
                HeaderResult.FirstOrDefault().lastmonth,
                HeaderResult.FirstOrDefault().beforelastmonth,
                HeaderResult.FirstOrDefault().thisvslastpercent,
                HeaderResult.FirstOrDefault().lastvsbeforelastpercent
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
                e.id,
                e.date,
                e.contractunit,
                e.start,
                e.end,
                e.duration,
                e.status
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
            punchHeatmap
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
            if (input.SelectTime < DateTime.Now)
                return Json(new { jobId = "", SelectTime = input.SelectTime, message = "登记失败,时间不能小于当前时间" });
            //早上9点之前立即执行
            var workStart = new TimeSpan(10, 0, 0);
            if (input.SelectTime.Value.TimeOfDay > workStart)
            {
                var rand = new Random();
                var offsetSeconds = rand.Next(0, 500);
                input.SelectTime = input.SelectTime.Value.AddSeconds(offsetSeconds);
            }
            else
            {
                var rand = new Random();
                var offsetSeconds = rand.Next(0, 59);
                input.SelectTime = input.SelectTime.Value.AddSeconds(offsetSeconds);
            }

            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var currentQuantity = dbConnection
              .Query<int>(
                $@"SELECT count(0) FROM public.autocheckinrecord where to_char(clockintime,'yyyy-mm-dd') = '{input.SelectTime.Value:yyyy-MM-dd}'")
              .First();
            if (currentQuantity >= 2)
                return Json(new { jobId = "", SelectTime = input.SelectTime, message = "登记失败,今日操作过于频繁" });
            var jobId = BackgroundJob.Schedule(() => attendanceService.AutoCheckIniclock(null), input.SelectTime.Value);
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
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
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
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var offset = (page - 1) * rows;
        return Json(dbConnection
          .Query<AutoCheckInRecord>(
            $@"SELECT * FROM public.autocheckinrecord ORDER BY clockintime desc LIMIT :rows OFFSET :offset",
            new { rows, offset }).ToList());
    }

    [Tags("考勤")]
    [EndpointSummary("获取周末加班数据")]
    [HttpPost]
    public ActionResult WorkingOvertimeOnWeekends(WorkingOvertimeOnWeekendsInput input)
    {
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var offset = (input.Page - 1) * input.Rows;
        return Json(dbConnection.Query<WorkingOvertimeOnWeekendsIOutput>($@"SELECT 
																		    name,
																		    to_char(clockintime, 'yyyy-MM-dd') as statisticsdate,
																		    MIN(clockintime) as signintime,
																		    MAX(clockintime) as signouttime,
																		    ROUND(EXTRACT(EPOCH FROM (MAX(clockintime) - MIN(clockintime))) / 3600, 1) as hoursdiff
																		FROM public.checkinwarning 
																		where
																			clockintime >= to_timestamp(:starttime,'yyyy-mm-dd hh24:mi:ss')
																			and clockintime <= to_timestamp(:endtime,'yyyy-mm-dd hh24:mi:ss')
																		GROUP BY name,to_char(clockintime, 'yyyy-MM-dd') 
																		ORDER BY {input.Order} {input.Sort}
																		 LIMIT :rows OFFSET :offset;",
          new
          {
              order = input.Order,
              rows = input.Rows,
              offset = offset,
              sort = input.Sort,
              starttime = input.StartTime + " 00:00:00",
              endtime = input.EndTime + " 23:59:59"
          }).ToList());
    }

    [Tags("考勤")]
    [EndpointSummary("工时排名")]
    [HttpPost]
    public ActionResult RankingOfWorkingHours(RankingOfWorkingHoursInput input)
    {
        using IDbConnection dbConnection = new MySqlConnection(configuration["OAConnection"]);
        var offset = (input.Page - 1) * input.Rows;
        return Json(dbConnection.Query<RankingOfWorkingHoursOutput>($@"WITH user_stats AS (
                                   SELECT 
                                       user_name,user_id,GROUP_CONCAT(DISTINCT org_name) as org_name,
                                       SUM(work_hours) as total_work_hours,
                                       SUM(work_overtime/60) as total_overtime,
                                       -- 工作工时排名
                                       RANK() OVER (ORDER BY SUM(work_hours) DESC) as work_rank,
                                       -- 加班排名
                                       RANK() OVER (ORDER BY SUM(work_overtime) DESC) as overtime_rank,
                                       COUNT(*) OVER () as total_users
                                   FROM hd_oa.oa_user_clock_in_record
                                   where clock_in_date BETWEEN @starttime AND @endtime
                                   GROUP BY user_name,user_id
                               )
                               SELECT 
                                   user_name as UserName,user_id as UserId,org_name as OrgName,
                                   total_work_hours as TotalWorkHours,
                                   total_overtime as TotalOvertime,
                                   -- 工作工时相关统计
                                   work_rank as WorkRank,
                                   total_users - work_rank as WorkSurpassedCount,
                                   ROUND((total_users - work_rank) * 100.0 / GREATEST(total_users - 1, 1), 2) as WorkSurpassedPercent,
                                   
                                   -- 加班相关统计
                                   overtime_rank as OvertimeRank,
                                   total_users - overtime_rank as OvertimeSurpassedCount,
                                   ROUND((total_users - overtime_rank) * 100.0 / GREATEST(total_users - 1, 1), 2) as OvertimeSurpassedPercent,
                                   
                                   -- 综合描述
                                   CONCAT('工时排名第', work_rank, '名，超越了', total_users - work_rank, '人(', 
                                          ROUND((total_users - work_rank) * 100.0 / GREATEST(total_users - 1, 1), 2), '%)；',
                                          '加班排名第', overtime_rank, '名，超越了', total_users - overtime_rank, '人(', 
                                          ROUND((total_users - overtime_rank) * 100.0 / GREATEST(total_users - 1, 1), 2), '%)') as RankDescription
                               FROM user_stats
                               where 1=1  {(string.IsNullOrEmpty(input.OrgName) ? "" : " AND org_name like @orgname ")} {(string.IsNullOrEmpty(input.UserName) ? "" : " AND user_name like @username ")}
                               order by  {input.Order} {input.Sort}
                                LIMIT {input.Rows} OFFSET {offset};",
          new
          {
              starttime = input.StartTime,
              endtime = input.EndTime,
              orgname = $"%{input.OrgName}%",
              username = $"%{input.UserName}%"
          }).ToList());
    }

    [Tags("考勤")]
    [EndpointSummary("考勤异常汇总")]
    [HttpPost]
    public ActionResult AttendanceAbnormalSummary(AttendanceAbnormalSummaryInput input)
    {
        using IDbConnection dbConnection = new MySqlConnection(configuration["OAConnection"]);
        var offset = (input.Page - 1) * input.Rows;

        // 验证排序字段和排序方式
        var validOrderFields = new[]
        {
      "UserId", "UserName", "OrgName", "MissingCardDays", "EarlyLeaveDays",
      "LateAndEarlyLeaveDays", "FieldLateDays", "FieldEarlyLeaveDays", "SupplementCardDays",
      "CompensatoryLeaveDays", "TotalLeaveDays", "TotalAbnormalDays"
    };
        var orderBy = validOrderFields.Contains(input.Order ?? "") ? input.Order : "TotalAbnormalDays";
        var sortOrder = (input.Sort?.ToLower() == "asc") ? "ASC" : "DESC";

        return Json(dbConnection.Query<AttendanceAbnormalSummaryOutput>($@"SELECT 
                r.user_id AS UserId, 
                r.user_name AS UserName, 
                r.org_id AS OrgId, 
                r.org_name AS OrgName,
                -- 核心异常指标:使用 DISTINCT 确保同一天多次相同异常只计为 1
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '缺卡' THEN r.clock_in_date END) AS MissingCardDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '早退' THEN r.clock_in_date END) AS EarlyLeaveDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '迟到' THEN r.clock_in_date END) AS LateAndEarlyLeaveDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '外勤/迟到' THEN r.clock_in_date END) AS FieldLateDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '外勤/早退' THEN r.clock_in_date END) AS FieldEarlyLeaveDays,
                -- 请假与补卡类
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '补卡' THEN r.clock_in_date END) AS SupplementCardDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '调休' THEN r.clock_in_date END) AS CompensatoryLeaveDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name IN ('事假','病假','婚假','丧假','产假','产检假','陪产假','哺乳假','路程假') THEN r.clock_in_date END) AS TotalLeaveDays,
                -- 总计:这一年该用户有多少天出现了异常
                COUNT(DISTINCT r.clock_in_date) AS TotalAbnormalDays
            FROM 
                oa_user_clock_in_record r
            JOIN 
                oa_user_clock_in_record_time t ON r.id = t.record_id
            WHERE 
                r.clock_in_date BETWEEN @starttime AND @endtime
                -- 排除掉要求的 5 种状态
                AND t.clock_in_status_name NOT IN ('正常', '打卡无效：此记录已被更新', '出差', '外勤')
                {(string.IsNullOrEmpty(input.OrgName) ? " AND r.org_id = '67' " : " AND r.org_name like @orgname ")}
                {(string.IsNullOrEmpty(input.UserName) ? "" : " AND r.user_name like @username ")}
            GROUP BY 
                r.user_id, r.user_name, r.org_id, r.org_name
            ORDER BY 
                {orderBy} {sortOrder}
            LIMIT {input.Rows} OFFSET {offset};",
          new
          {
              starttime = input.StartTime,
              endtime = input.EndTime,
              orgname = $"%{input.OrgName}%",
              username = $"%{input.UserName}%"
          }).ToList());
    }

    [Tags("考勤")]
    [EndpointSummary("考勤异常明细")]
    [HttpPost]
    public ActionResult AttendanceAbnormalDetail(AttendanceAbnormalDetailInput input)
    {
        using IDbConnection dbConnection = new MySqlConnection(configuration["OAConnection"]);
        var offset = (input.Page - 1) * input.Rows;

        // 验证排序字段和排序方式
        var validOrderFields = new[] { "ClockInDate", "UserName", "TotalAbnormalMinutes" };
        var orderBy = validOrderFields.Contains(input.Order ?? "") ? input.Order : "ClockInDate";
        var sortOrder = (input.Sort?.ToLower() == "asc") ? "ASC" : "DESC";

        return Json(dbConnection.Query<AttendanceAbnormalDetailOutput>($@"SELECT 
                r.clock_in_date AS ClockInDate,
                r.user_name AS UserName,
                -- 将打卡时间合并,并按时间先后排序
                GROUP_CONCAT(DATE_FORMAT(t.clock_in_time, '%H:%i:%s') ORDER BY t.clock_in_time SEPARATOR ' | ') AS ActualClockInTime,
                -- 将异常状态合并
                GROUP_CONCAT(t.clock_in_status_name ORDER BY t.clock_in_time SEPARATOR ' | ') AS AbnormalStatus,
                SUM(t.later_early_minutes) AS TotalAbnormalMinutes,
                GROUP_CONCAT(t.remark SEPARATOR '; ') AS RemarkSummary
            FROM 
                oa_user_clock_in_record r
            JOIN 
                oa_user_clock_in_record_time t ON r.id = t.record_id
            WHERE 
                r.clock_in_date BETWEEN @starttime AND @endtime
                -- 排除掉不需要显示的5种状态
                AND t.clock_in_status_name NOT IN ('正常', '打卡无效：此记录已被更新', '出差', '外勤')
                {(string.IsNullOrEmpty(input.UserId) ? "" : " AND r.user_id = @userid ")}
                {(string.IsNullOrEmpty(input.OrgId) ? "" : " AND r.org_id = @orgid ")}
                {(string.IsNullOrEmpty(input.UserName) ? "" : " AND r.user_name like @username ")}
            GROUP BY 
                r.clock_in_date, r.day_of_week, r.team_name, r.user_name
            ORDER BY 
                {orderBy} {sortOrder}
            LIMIT {input.Rows} OFFSET {offset};",
          new
          {
              starttime = input.StartTime,
              endtime = input.EndTime,
              userid = input.UserId,
              orgid = input.OrgId,
              username = $"%{input.UserName}%"
          }).ToList());
    }

    [Tags("考勤")]
    [EndpointSummary("公司范围动作触发(0进入，1离开)")]
    [HttpGet]
    public async Task<ActionResult> RangeAction(int Type)
    {
        if (Type == 0) return await HandleEnterRange();
        if (Type == 1) return await HandleLeaveRange();
        return Json(new { success = false, message = "参数错误" });
    }
    [Tags("考勤")]
    [EndpointSummary("测试-公司范围动作触发(0进入，1离开)")]
    [HttpGet]
    public async Task<ActionResult> RangeActionTest(int Type)
    {
        if (Type == 0)
        {
            pushMessageService.Push("测试提醒", "Type" + Type, PushMessageService.PushIcon.Windows);
            return Json(new { success = true, message = "设备已开启", isWorking = 1 });
        }
        if (Type == 1)
        {
             pushMessageService.Push("测试提醒", "Type" + Type, PushMessageService.PushIcon.Windows);
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
                }, new Dictionary<string, string> { { "Authorization", homeAssistantInfo.Authorization ?? string.Empty } });

                if (response.IsSuccessStatusCode)
                {
                    pushMessageService.Push("开机提醒", "已为您开启电脑", PushMessageService.PushIcon.Windows);
                    return Json(new { success = true, message = "设备已开启", isWorking = 1 });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"调用Home Assistant失败: {errorContent}", isWorking = 1 });
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
                $"SELECT COUNT(0) FROM public.autocheckinrecord WHERE to_char(clockintime,'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}' and clockinstate = 0 ").First();
            if (existautocheckin > 0)
            {
                return Json(new { success = true });
            }

            // 2. 获取今日工时
            var workHours = dbConnection.Query<double>(
                "SELECT workhours FROM public.attendancerecordday WHERE attendancedate::date = CURRENT_DATE LIMIT 1").FirstOrDefault();

            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;

            if (workHours > 0)
            {
                // 3. 工时大于0，调用关机接口
                if (!string.IsNullOrEmpty(pmisInfo.ShutDownUrl))
                {
                    try
                    {
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        var shutDownResponse = await client.GetAsync(pmisInfo.ShutDownUrl);
                        if (shutDownResponse.IsSuccessStatusCode)
                        {
                            pushMessageService.Push("关机提醒", "您的电脑即将关机", PushMessageService.PushIcon.Close);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 忽略关机接口调用失败，可能是机器已关机
                        Console.WriteLine($"调用关机接口失败: {ex.Message}");
                    }
                }
                return Json(new { success = true });
            }
            else
            {
                var response = await httpRequestHelper.PostAsync(pmisInfo.ZkUrl + "/api/v2/transaction/get/?key=" + pmisInfo.ZkKey,
                    new
                    {
                        starttime = DateTime.Now.ToString("yyyy-MM-dd 00:00:00"),
                        endtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });

                var result = await response.Content.ReadAsStringAsync();
                var resultModel = JsonConvert.DeserializeObject<ZktResponse>(result);
                var pin = "100" + pmisInfo.UserAccount;
                var myRecordsCount = resultModel?.Data?.Items?.Count(e => e.Pin == pin) ?? 0;

                if (myRecordsCount >= 2)
                {
                    if (!string.IsNullOrEmpty(pmisInfo.ShutDownUrl))
                    {
                        try
                        {
                            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                            var shutDownResponse = await client.GetAsync(pmisInfo.ShutDownUrl);
                            if (shutDownResponse.IsSuccessStatusCode)
                            {
                                pushMessageService.Push("关机提醒", "您的电脑即将关机,已为您触发考勤同步", PushMessageService.PushIcon.Close);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 忽略关机接口调用失败，可能是机器已关机
                            Console.WriteLine($"调用关机接口失败: {ex.Message}");
                        }
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
}
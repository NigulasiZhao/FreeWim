using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using FreeWim.Models;
using FreeWim.Models.Attendance;
using System.Data;
using Dapper;
using Npgsql;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AttendanceRecordController : Controller
{
    private readonly IConfiguration _Configuration;

    public AttendanceRecordController(IConfiguration configuration)
    {
        _Configuration = configuration;
    }

    [Tags("考勤")]
    [EndpointSummary("考勤组件数据查询接口")]
    [HttpGet]
    public ActionResult latest()
    {
        IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
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
        IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
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
    [EndpointSummary("获取考勤面板数据")]
    [HttpGet]
    public ActionResult GetBoardData()
    {
        IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
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
        WHERE attendancedate >= date_trunc('month', CURRENT_DATE) - INTERVAL '5 months'
        GROUP BY TO_CHAR(attendancedate, 'YYYY-MM')
        ORDER BY Month;
    ";

        var result = _DbConnection.Query<(string Month, double TotalHours, double OvertimeHours)>(sql);

        var sqlCheckIn = @"SELECT
    TO_CHAR(attendancedate::date, 'MM月DD日') AS DayLabel,
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
            TO_CHAR(attendancedate::date, 'MM月DD日') AS DayLabel,
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
        var endresult = new
        {
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
}
using System.Data;
using Dapper;
using FreeWim.Models.PmisAndZentao;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Npgsql;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AnnualReportController(IConfiguration configuration) : Controller
{
    [HttpGet]
    public IActionResult GetSummary(int year)
    {
        if (year == 0) year = DateTime.Now.Year;
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        using IDbConnection db = new NpgsqlConnection(configuration["Connection"]);

        // 1. Attendance Data
        var workDays = db.QueryFirstOrDefault<int>(@"
            SELECT COUNT(DISTINCT attendancedate::date) 
            FROM attendancerecorddaydetail 
            WHERE EXTRACT(YEAR FROM attendancedate) = @year AND clockintype IN ('0', '1')", new { year });

        var totalHours = db.QueryFirstOrDefault<double>(@"
            SELECT SUM(workhours) 
            FROM attendancerecordday 
            WHERE EXTRACT(YEAR FROM attendancedate) = @year", new { year });

        var earliestCheckIn = db.QueryFirstOrDefault<dynamic>(@"
            SELECT attendancedate, clockintime 
            FROM attendancerecorddaydetail 
            WHERE EXTRACT(YEAR FROM attendancedate) = @year AND clockintype = '0' and clockintime is not null and to_char(clockintime,'yyyy-MM-dd') <> '0001-01-01'
            ORDER BY clockintime::time ASC LIMIT 1", new { year });

        var latestCheckOut = db.QueryFirstOrDefault<dynamic>(@"
            SELECT attendancedate, clockintime 
            FROM attendancerecorddaydetail 
            WHERE EXTRACT(YEAR FROM attendancedate) = @year AND clockintype = '1' and clockintime is not null and to_char(clockintime,'yyyy-MM-dd') <> '0001-01-01'
            ORDER BY clockintime::time DESC LIMIT 1", new { year });

        var longestDay = db.QueryFirstOrDefault<dynamic>(@"
            SELECT attendancedate, workhours 
            FROM attendancerecordday 
            WHERE EXTRACT(YEAR FROM attendancedate) = @year 
            ORDER BY workhours DESC LIMIT 1", new { year });

        // 2. Overtime & Ranking Data (From OA)
        dynamic? rankingData = null;
        if (!string.IsNullOrEmpty(pmisInfo.UserName))
        {
            using IDbConnection oaDb = new MySqlConnection(configuration["OAConnection"]);
            var starttime = $"{year}-01-01";
            var endtime = $"{year}-12-31";

            var rankingList = oaDb.Query<dynamic>($@"
                    WITH user_stats AS (
                        SELECT 
                            user_name, user_id,
                            SUM(work_hours) as total_work_hours,
                            SUM(work_overtime/60) as total_overtime,
                            SUM(CASE WHEN work_overtime > 0 THEN 1 ELSE 0 END) as overtime_count,
                            RANK() OVER (ORDER BY SUM(work_hours) DESC) as work_rank,
                            RANK() OVER (ORDER BY SUM(work_overtime) DESC) as overtime_rank,
                            COUNT(*) OVER () as total_users
                        FROM hd_oa.oa_user_clock_in_record
                        WHERE clock_in_date BETWEEN @starttime AND @endtime
                        GROUP BY user_name, user_id
                    )
                    SELECT 
                        total_work_hours,
                        total_overtime,
                        overtime_count,
                        work_rank,
                        overtime_rank,
                        total_users,
                        CONCAT('工时排名第', work_rank, '名，超越了', total_users - work_rank, '人(', 
                               ROUND((total_users - work_rank) * 100.0 / GREATEST(total_users - 1, 1), 2), '%)；',
                               '加班排名第', overtime_rank, '名，超越了', total_users - overtime_rank, '人(', 
                               ROUND((total_users - overtime_rank) * 100.0 / GREATEST(total_users - 1, 1), 2), '%)') as RankDescription
                    FROM user_stats
                    WHERE user_name LIKE @username
                    LIMIT 1;",
                    new { starttime, endtime, username = pmisInfo.UserName });

            rankingData = rankingList.FirstOrDefault();
        }

        // 3. Task Data (Zentao)
        var taskStats = db.QueryFirstOrDefault<dynamic>(@"
            SELECT COUNT(*) as total, SUM(consumed) as consumed
            FROM zentaotask 
            WHERE EXTRACT(YEAR FROM eststarted) = @year", new { year });

        // 3.1 Top Project & Task
        var topProject = db.QueryFirstOrDefault<dynamic>(@"
            SELECT projectname, SUM(consumed) as consumed
            FROM zentaotask 
            WHERE EXTRACT(YEAR FROM eststarted) = @year AND projectname IS NOT NULL
            GROUP BY projectname
            ORDER BY consumed DESC
            LIMIT 1", new { year });

        var topTask = db.QueryFirstOrDefault<dynamic>(@"
            SELECT taskname as name, consumed, projectname
            FROM zentaotask 
            WHERE EXTRACT(YEAR FROM eststarted) = @year AND taskname IS NOT NULL
            ORDER BY consumed DESC
            LIMIT 1", new { year });

        // 4. Network Data
        var netStats = db.QueryFirstOrDefault<dynamic>(@"
            SELECT AVG(download) as download, AVG(upload) as upload, MAX(download) as max_download
            FROM speedrecord 
            WHERE EXTRACT(YEAR FROM created_at) = @year", new { year });

        // 5. Keep Data
        var keepCount = db.QueryFirstOrDefault<int>(@"
            SELECT COUNT(*) 
            FROM eventinfo 
            WHERE source = 'keep' AND EXTRACT(YEAR FROM clockintime) = @year", new { year });

        // 6. Gogs/Git Data
        var commitCount = db.QueryFirstOrDefault<int>(@"
            SELECT COUNT(*)
            FROM gogsrecord
            WHERE EXTRACT(YEAR FROM commitsdate) = @year", new { year });

      

        // 7. Automation Data
        var autoCheckInStats = db.QueryFirstOrDefault<dynamic>(@"
            SELECT 
                COUNT(*) FILTER (WHERE clockinstate = 1) as success_count,
                COUNT(*) as total_count,
                MIN(clockintime::time) as earliest_time,
                MAX(clockintime::time) as latest_time
            FROM autocheckinrecord 
            WHERE EXTRACT(YEAR FROM clockintime) = @year", new { year });

        var autoCheckInCount = (int)(autoCheckInStats?.success_count ?? 0);
        var autoCheckInTotal = (int)(autoCheckInStats?.total_count ?? 0);
        var checkInSuccessRate =
            autoCheckInTotal > 0 ? Math.Round((double)autoCheckInCount / autoCheckInTotal * 100, 1) : 0;
        var earliestAutoCheckIn = autoCheckInStats?.earliest_time != null
            ? ((TimeSpan)autoCheckInStats.earliest_time).ToString(@"hh\:mm")
            : "-";
        var latestAutoCheckIn = autoCheckInStats?.latest_time != null
            ? ((TimeSpan)autoCheckInStats.latest_time).ToString(@"hh\:mm")
            : "-";

        var autoDailyReports = db.QueryFirstOrDefault<int>(@"
            SELECT COUNT(*) 
            FROM attendancerecordday 
            WHERE workhours > 0 AND EXTRACT(YEAR FROM attendancedate) = @year", new { year });
        var autoWeeklyReports = autoDailyReports / 5;
        var totalGeneratedWords = (autoDailyReports * 300) + (autoWeeklyReports * 800);

        var aiTaskCompletions = db.QueryFirstOrDefault<int>(@"
            SELECT COUNT(*) 
            FROM zentaotask 
            WHERE target IS NOT NULL AND planfinishact IS NOT NULL AND EXTRACT(YEAR FROM eststarted) = @year",
            new { year });

        var autoOvertimeApps = (int)(rankingData?.overtime_count ?? 0);
        var savedMinutes = (autoCheckInCount * 1) + (autoDailyReports * 5) + (autoWeeklyReports * 20) +
                           (aiTaskCompletions * 3) + (autoOvertimeApps * 5);
        var savedHours = Math.Round((double)savedMinutes / 60, 1);

        var maxCommitsDay = db.QueryFirstOrDefault<dynamic>(@"
            SELECT commitsdate::date as date, COUNT(*) as count 
            FROM gogsrecord 
            WHERE EXTRACT(YEAR FROM commitsdate) = @year 
            GROUP BY commitsdate::date 
            ORDER BY count DESC 
            LIMIT 1", new { year });

        var repoStats = db.Query<dynamic>(@"
            SELECT repositoryname, COUNT(*) as count 
            FROM gogsrecord 
            WHERE EXTRACT(YEAR FROM commitsdate) = @year 
            GROUP BY repositoryname 
            HAVING repositoryname IS NOT NULL
            ORDER BY count DESC 
            LIMIT 5", new { year });

        var latestCommit = db.QueryFirstOrDefault<dynamic>(@"
            SELECT commitsdate 
            FROM gogsrecord 
            WHERE EXTRACT(YEAR FROM commitsdate) = @year 
            ORDER BY commitsdate::time DESC 
            LIMIT 1", new { year });

        return Json(new
        {
            Year = year,
            pmisInfo.UserName,
            Attendance = new
            {
                WorkDays = workDays,
                TotalHours = Math.Round(totalHours, 1),
                AverageDailyHours = workDays > 0 ? Math.Round(totalHours / workDays, 1) : 0,
                EarliestCheckIn = earliestCheckIn != null
                    ? new
                    {
                        Date = ((DateTime)earliestCheckIn.attendancedate).ToString("MM-dd"),
                        Time = ((DateTime)earliestCheckIn.clockintime).ToString("HH:mm")
                    }
                    : null,
                LatestCheckOut = latestCheckOut != null
                    ? new
                    {
                        Date = ((DateTime)latestCheckOut.attendancedate).ToString("MM-dd"),
                        Time = ((DateTime)latestCheckOut.clockintime).ToString("HH:mm")
                    }
                    : null,
                LongestDay = longestDay != null
                    ? new
                    {
                        Date = ((DateTime)longestDay.attendancedate).ToString("MM-dd"), Hours = longestDay.workhours
                    }
                    : null
            },
            Overtime = new
            {
                Count = (int)(rankingData?.overtime_count ?? 0),
                TotalHours = (double)(rankingData?.total_overtime ?? 0),
                RankDescription = (string)(rankingData?.RankDescription ?? ""),
                RankingInfo = rankingData != null ? new {
                    WorkRank = (int)rankingData.work_rank,
                    OvertimeRank = (int)rankingData.overtime_rank,
                    TotalUsers = (int)rankingData.total_users,
                   WorkBeatRate = (long)rankingData.total_users > 1
                       ? Math.Round(
                           ((long)rankingData.total_users - (long)rankingData.work_rank) * 100.0 /
                           ((long)rankingData.total_users - 1), 1)
                       : 100,

                   OvertimeBeatRate = (long)rankingData.total_users > 1
                       ? Math.Round(
                           ((long)rankingData.total_users - (long)rankingData.overtime_rank) * 100.0 /
                           ((long)rankingData.total_users - 1), 1)
                       : 100
                } : null
            },
            Tasks = new
            {
                Total = taskStats?.total ?? 0,
                Consumed = taskStats?.consumed ?? 0,
                TopProject = topProject != null ? new { Name = (string)topProject.projectname, Consumed = (double)topProject.consumed } : null,
                TopTask = topTask != null ? new { Name = (string)topTask.name, Consumed = (double)topTask.consumed, Project = (string)topTask.projectname } : null
            },
            Network = new
            {
                Download = netStats?.download != null ? Math.Round((double)netStats.download, 1) : 0,
                Upload = netStats?.upload != null ? Math.Round((double)netStats.upload, 1) : 0,
                MaxDownload = netStats?.max_download != null ? Math.Round((double)netStats.max_download, 1) : 0
            },
            Keep = new
            {
                Count = keepCount
            },
            Code = new
            {
                Commits = commitCount,
                MaxDay = maxCommitsDay != null
                    ? new { Date = ((DateTime)maxCommitsDay.date).ToString("MM-dd"), Count = (int)maxCommitsDay.count }
                    : null,
                RepoStats = repoStats.Select(r => new { Name = (string)r.repositoryname, Count = (int)r.count })
                    .ToList(),
                LatestCommit = latestCommit != null
                    ? new
                    {
                        Date = ((DateTime)latestCommit.commitsdate).ToString("MM-dd"),
                        Time = ((DateTime)latestCommit.commitsdate).ToString("HH:mm")
                    }
                    : null
            },
            Automation = new
            {
                SavedHours = savedHours,
                CheckIn = new
                {
                    Count = autoCheckInCount,
                    SuccessRate = checkInSuccessRate,
                    EarliestTime = earliestAutoCheckIn,
                    LatestTime = latestAutoCheckIn
                },
                Reports = new
                {
                    Daily = autoDailyReports,
                    Weekly = autoWeeklyReports,
                    TotalWords = totalGeneratedWords
                },
                Overtime = new
                {
                    Count = autoOvertimeApps,
                    SavedMinutes = autoOvertimeApps * 5
                },
                TaskCompletion = aiTaskCompletions
            }
        });
    }
}

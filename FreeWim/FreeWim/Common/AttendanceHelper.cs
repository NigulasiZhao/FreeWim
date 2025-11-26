using System.Data;
using System.Text;
using Dapper;
using Npgsql;
using FreeWim.Models.Attendance;
using FreeWim.Models.PmisAndZentao;
using Hangfire.Server;

namespace FreeWim.Common;

public class AttendanceHelper(IConfiguration configuration, ILogger<ZentaoHelper> logger)
{
    /// <summary>
    /// 根据日期获取当日工时
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public double GetWorkHoursByDate(DateTime date)
    {
        double hours = 0;
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var isSignout = dbConnection.Query<int>($@"select count(0) from public.attendancerecordday where to_char(attendancedate,'yyyy-MM-dd') = '{date:yyyy-MM-dd}' and workhours > 0 ").First();
        if (isSignout <= 0) return hours;
        var attendanceList = dbConnection.Query<WorkHoursInOutTime>($@"select
                                            	clockintype,
                                            	max(clockintime) as clockintime
                                            from
                                            	public.attendancerecorddaydetail
                                            where
                                            	to_char(attendancedate,'yyyy-MM-dd') = '{date:yyyy-MM-dd}'
                                            group by
                                            	clockintype").ToList();
        DateTime? signInDate = null;
        DateTime? signOutDate = null;
        if (attendanceList.FirstOrDefault(e => e.ClockInType == 0) != null) signInDate = attendanceList.FirstOrDefault(e => e.ClockInType == 0)?.ClockInTime;
        if (attendanceList.FirstOrDefault(e => e.ClockInType == 1) != null) signOutDate = attendanceList.FirstOrDefault(e => e.ClockInType == 1)?.ClockInTime;
        if (signInDate == null || signOutDate == null) return hours;
        signInDate = RoundToHalfHour(signInDate.Value, RoundDirection.Up);
        signOutDate = RoundToHalfHour(signOutDate.Value, RoundDirection.Down);
        hours = (signOutDate.Value - signInDate.Value).TotalHours;

        var noonStart = new DateTime(signInDate.Value.Year, signInDate.Value.Month, signInDate.Value.Day, 12, 0, 0);
        var noonEnd = new DateTime(signInDate.Value.Year, signInDate.Value.Month, signInDate.Value.Day, 13, 0, 0);

        // 计算时间段与午休时间的重叠
        var overlapStart = signInDate > noonStart ? signInDate : noonStart;
        var overlapEnd = signOutDate < noonEnd ? signOutDate : noonEnd;

        double overlapHours = 0;
        if (overlapStart < overlapEnd) overlapHours = (overlapEnd.Value - overlapStart.Value).TotalHours;
        hours = hours - overlapHours;

        // return hours - overlapHours;
        return hours;
    }

    /// <summary>
    /// 时间取整处理
    /// </summary>
    /// <param name="dt"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DateTime RoundToHalfHour(DateTime dt, RoundDirection direction)
    {
        var minute = dt.Minute;
        var second = dt.Second;
        var millisecond = dt.Millisecond;

        switch (direction)
        {
            case RoundDirection.Up:
                if (minute == 0 || minute == 30)
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, minute, 0);
                if (minute < 30)
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 30, 0);
                else
                    return new DateTime(dt.AddHours(1).Year, dt.AddHours(1).Month, dt.AddHours(1).Day, dt.AddHours(1).Hour, 0, 0);

            case RoundDirection.Down:
                var roundedMinute = minute < 30 ? 0 : 30;
                return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, roundedMinute, 0);

            case RoundDirection.Nearest:
                if (minute < 15)
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);
                else if (minute < 45)
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 30, 0);
                else
                    return new DateTime(dt.AddHours(1).Year, dt.AddHours(1).Month, dt.AddHours(1).Day, dt.AddHours(1).Hour, 0, 0);

            default:
                throw new ArgumentException("Unsupported round direction.");
        }
    }

    public enum RoundDirection
    {
        Up,
        Down,
        Nearest
    }

    public void AutoCheckIniclock(PerformContext context)
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var jobId = context.BackgroundJob.Id;
        if (!string.IsNullOrEmpty(jobId))
        {
            var autoCheckInRecord = dbConnection.Query<AutoCheckInRecord>($@"SELECT * FROM public.autocheckinrecord WHERE jobid = '{jobId}'").FirstOrDefault();
            if (autoCheckInRecord != null)
            {
                var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
                var url = $"{pmisInfo.ZkUrl}/iclock/cdata?SN={pmisInfo.ZkSN}&table=ATTLOG&Stamp=9999";
                var contentString = $"100{pmisInfo.UserAccount}\t{autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss}\t0\t15\t0\t0\t0";
                // using var client = new HttpClient();
                // var content = new StringContent(contentString, Encoding.UTF8, "text/plain");
                // var response = client.PostAsync(url, content).Result;
                // var result = response.Content.ReadAsStringAsync().Result;
                // if (response.StatusCode == System.Net.HttpStatusCode.OK)
                //     dbConnection.Execute(result.Contains("OK:1")
                //         ? $@"UPDATE public.autocheckinrecord SET clockinstate = 1,updateat = now() WHERE jobid = '{jobId}'"
                //         : $@"UPDATE public.autocheckinrecord SET clockinstate = 2,updateat = now() WHERE jobid = '{jobId}'");
                // else
                dbConnection.Execute($@"UPDATE public.autocheckinrecord SET clockinstate = 2 ,updateat = now() WHERE jobid = '{jobId}'");
            }
        }

        dbConnection.Dispose();
    }
}
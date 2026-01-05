using System.Data;
using System.Text;
using Dapper;
using Npgsql;
using FreeWim.Models.Attendance;
using FreeWim.Models.Attendance.Dto;
using FreeWim.Models.PmisAndZentao;
using Hangfire.Server;
using Newtonsoft.Json;
using FreeWim.Utils;

namespace FreeWim.Services;

public class AttendanceService(IConfiguration configuration, PushMessageService pushMessageService, TokenService tokenService, WorkFlowExecutorService workFlowExecutorService, PmisService pmisService)
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

    public void AutoCheckIniclock(PerformContext? context)
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var jobId = context?.BackgroundJob.Id;
        if (!string.IsNullOrEmpty(jobId))
        {
            var autoCheckInRecord = dbConnection.Query<AutoCheckInRecord>($@"SELECT * FROM public.autocheckinrecord WHERE jobid = '{jobId}'").FirstOrDefault();
            if (autoCheckInRecord != null)
            {
                var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
                var url = $"{pmisInfo.ZkUrl}/iclock/cdata?SN={pmisInfo.ZkSN}&table=ATTLOG&Stamp=9999";
                var contentString = $"100{pmisInfo.UserAccount}\t{autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss}\t0\t15\t0\t0\t0";
                using var client = new HttpClient();
                var content = new StringContent(contentString, Encoding.UTF8, "text/plain");
                var response = client.PostAsync(url, content).Result;
                var result = response.Content.ReadAsStringAsync().Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (result.Contains("OK:1"))
                    {
                        dbConnection.Execute($@"UPDATE public.autocheckinrecord SET clockinstate = 1,updateat = now() WHERE jobid = '{jobId}'");
                        pushMessageService.Push("任务调度", $"您设定于 {autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss} 执行的任务已执行，请关注后续考勤同步信息。", PushMessageService.PushIcon.Zktime);
                    }
                    else
                    {
                        dbConnection.Execute($@"UPDATE public.autocheckinrecord SET clockinstate = 2,updateat = now() WHERE jobid = '{jobId}'");
                        pushMessageService.Push("任务调度", $"您设定于 {autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss} 执行的任务未能成功完成。\n失败原因：" + result, PushMessageService.PushIcon.Alert);
                    }
                }
                else
                {
                    dbConnection.Execute($@"UPDATE public.autocheckinrecord SET clockinstate = 2 ,updateat = now() WHERE jobid = '{jobId}'");
                    pushMessageService.Push("任务调度", $"您设定于 {autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss} 执行的任务未能成功完成。\n接口调用失败：" + result, PushMessageService.PushIcon.Alert);
                }
            }
        }

        dbConnection.Dispose();
    }

    /// <summary>
    /// 高危人员打卡预警
    /// 每5分钟执行一次，查询当前是否为休息日，如果是休息日则进行token伪造，同时根据配置中的人员名单查询名单内人员的考勤记录；如存在高危人员打卡，则进行消息提醒，同时将提醒过的内容写入缓存，缓存有效期20小时，避免重复提醒
    /// </summary>
    public void CheckInWarning()
    {
        if (DateTime.Now.Hour <= 7 || DateTime.Now.Hour >= 23) return;
        var httpRequestHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var lastDay = dbConnection.Query<string>($@"select
                                                                                                	checkinrule
                                                                                                from
                                                                                                	public.attendancerecordday
                                                                                                where
                                                                                                	to_char(attendancedate,
                                                                                                	'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}'").FirstOrDefault();
        if (lastDay == null) return;
        if (lastDay != "休息") return;
        var pushMessage = "";
        var listOfPersonnel = configuration.GetSection("ListOfPersonnel").Get<List<ListOfPersonnel>>();
        if (listOfPersonnel != null)
        {
            var realNameList = listOfPersonnel.Select(e => e.RealName).ToList();
            var response = httpRequestHelper.PostAsync(pmisInfo.ZkUrl + "/api/v2/transaction/get/?key=" + pmisInfo.ZkKey,
                new
                {
                    starttime = DateTime.Now.AddMinutes(-10).ToString("yyyy-MM-dd HH:mm:ss"),
                    endtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }).Result;
            var result = response.Content.ReadAsStringAsync().Result;
            var resultModel = JsonConvert.DeserializeObject<ZktResponse>(result);
            if (resultModel?.Data is { Count: > 0 })
            {
                var personList = resultModel.Data.Items?.Where(e => e is { Alias: "郑州", Deptname: "郑州驻外办" }).ToList();
                if (personList is { Count: > 0 })
                    foreach (var person in personList)
                    {
                        var checktime = person.Checktime;
                        var waringcount = dbConnection.Query<int>(
                                $@"SELECT COUNT(0) FROM public.checkinwarning WHERE name = '{person.Ename}' AND clockintime = '{checktime}'")
                            .First();
                        if (waringcount > 0) continue;
                        if (checktime == null) continue;
                        if (listOfPersonnel.FirstOrDefault(e => e.RealName == person.Ename) != null)
                            pushMessage += listOfPersonnel.FirstOrDefault(e => e.RealName == person.Ename)!.FlowerName + "-打卡时间:" +
                                           DateTime.Parse(checktime).ToString("HH:mm:ss") + "\n";
                        dbConnection.Execute($@"INSERT INTO public.checkinwarning(id,name,clockintime) VALUES('{Guid.NewGuid()}','{person.Ename}','{checktime}')");
                    }
            }
        }

        if (string.IsNullOrEmpty(pushMessage)) return;
        pushMessageService.Push("高危人员打卡提醒", pushMessage, PushMessageService.PushIcon.Camera);
    }

    /// <summary>
    /// 考勤数据同步
    /// 每小时的5分，35分调用一次查询当月考勤数据接口；接口结果对比本地库中数据，如果接口返回有新数据，则更新本地数据库
    /// 考勤获取到当日签退记录后，则执行禅道关闭工单，日报发送，周报发送任务
    /// </summary>
    public void SyncAttendanceRecord()
    {
        var signout = false;
        var pushMessage = "";
        var insertIdent = false;
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", tokenService.GetTokenAsync());
        var startDate = DateTime.Now;
        if (DateTime.Now.Hour <= 7 || DateTime.Now.Hour >= 23) return;
        
        var response = client.GetAsync(pmisInfo!.Url + "/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + startDate.ToString("yyyy-MM")).Result;
        var result = response.Content.ReadAsStringAsync().Result;
        var resultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
        if (resultModel is { Code: 200 })
        {
            //查询已有的今日打卡记录
            var todayAttendanceList = dbConnection.Query<WorkHoursInOutTime>($@"select
                                            	clockintype,
                                            	clockIntime
                                            from
                                            	public.attendancerecorddaydetail
                                            where
                                            	to_char(attendancedate,'yyyy-MM-dd') = '{startDate:yyyy-MM-dd}'
                                            ").ToList();
            if (resultModel.Data != null)
                if (resultModel.Data.DayVoList != null)
                    if (resultModel.Data.DayVoList.Count > 0)
                        foreach (var daydetail in resultModel.Data.DayVoList.Where(e => e.Day == startDate.Day))
                            if (daydetail.DetailList != null)
                                foreach (var daydetailitem in daydetail.DetailList)
                                    if (!todayAttendanceList.Any(e => daydetailitem.ClockInType != null &&
                                                                      e.ClockInType == int.Parse(daydetailitem.ClockInType) &&
                                                                      e.ClockInTime == (DateTime.TryParse(daydetailitem.ClockInTime, out var parsedDate) ? parsedDate : null)))
                                    {
                                        insertIdent = true;
                                        if (string.IsNullOrEmpty(daydetailitem.ClockInTime)) continue;
                                        pushMessage = "数据已同步\n" + (daydetailitem.ClockInType != null && int.Parse(daydetailitem.ClockInType) == 0 ? "签到时间:" : "签退时间:") + daydetailitem.ClockInTime;
                                        if (daydetailitem.ClockInType != null && int.Parse(daydetailitem.ClockInType) == 1) signout = true;
                                    }

            if (insertIdent)
            {
                dbConnection.Execute($"delete from public.attendancerecord where attendancemonth = '{startDate:yyyy-MM}'");
                dbConnection.Execute($"delete from public.attendancerecordday where to_char(attendancedate,'yyyy-mm') = '{startDate:yyyy-MM}'");
                dbConnection.Execute($"delete from public.attendancerecorddaydetail where to_char(attendancedate,'yyyy-mm') = '{startDate:yyyy-MM}'");
                if (resultModel.Data != null)
                {
                    dbConnection.Execute(
                        $"INSERT INTO public.attendancerecord(attendancemonth,workdays,latedays,earlydays) VALUES('{startDate:yyyy-MM}',{resultModel.Data.WorkDays},{resultModel.Data.LateDays},{resultModel.Data.EarlyDays});");
                    if (resultModel.Data.DayVoList != null)
                        foreach (var item in resultModel.Data.DayVoList)
                        {
                            var flagedate = DateTime.Parse(startDate.ToString("yyyy-MM") + "-" + item.Day);
                            dbConnection.Execute($"""
                                                  INSERT INTO public.attendancerecordday(untilthisday,day,checkinrule,isnormal,isabnormal,isapply,clockinnumber,workhours,attendancedate,yearmonth)
                                                                                                          VALUES({item.UntilThisDay},{item.Day},'{item.CheckInRule}','{item.IsNormal}','{item.IsAbnormal}','{item.IsApply}',{item.ClockInNumber},{(item.WorkHours == null ? 0 : item.WorkHours)},to_timestamp('{flagedate:yyyy-MM-dd 00:00:00}', 'yyyy-mm-dd hh24:mi:ss'),'{startDate:yyyy-MM}');
                                                  """);
                            if (item.DetailList != null)
                                foreach (var daydetail in item.DetailList)
                                    dbConnection.Execute($"""
                                                              INSERT INTO public.attendancerecorddaydetail
                                                                  (id, recordid, clockintype, clockintime, attendancedate)
                                                              VALUES
                                                                  ({daydetail.Id},
                                                                   {daydetail.RecordId},
                                                                   '{daydetail.ClockInType}',
                                                                   {(string.IsNullOrEmpty(daydetail.ClockInTime) ? "null" : $"to_timestamp('{daydetail.ClockInTime:yyyy-MM-dd HH:mm:ss}', 'yyyy-mm-dd hh24:mi:ss')")},
                                                                   to_timestamp('{flagedate:yyyy-MM-dd 00:00:00}', 'yyyy-mm-dd hh24:mi:ss'));
                                                          """);
                        }
                }

                if (!string.IsNullOrEmpty(pushMessage)) pushMessageService.Push("考勤", pushMessage, PushMessageService.PushIcon.Attendance);
                if (signout)
                {
                    workFlowExecutorService.ExecuteAll();
                    pmisService.RealOverTimeList();
                }
            }
        }

        var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
        if (startDate.Day >= daysInMonth - 1)
        {
            var lastMonthData = dbConnection.Query<int>($@"select count(0) from public.attendancerecordday where yearmonth = '{startDate.AddMonths(1):yyyy-MM}'").First();
            if (lastMonthData == 0)
            {
                var lastresponse = client.GetAsync(pmisInfo!.Url + "/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + startDate.AddMonths(1).ToString("yyyy-MM")).Result;
                var lastresult = lastresponse.Content.ReadAsStringAsync().Result;
                var lastresultModel = JsonConvert.DeserializeObject<AttendanceResponse>(lastresult);
                if (lastresultModel is { Code: 200, Data.DayVoList.Count: > 0 })
                {
                    dbConnection.Execute(
                        $"INSERT INTO public.attendancerecord(attendancemonth,workdays,latedays,earlydays) VALUES('{startDate.AddMonths(1):yyyy-MM}',{lastresultModel.Data.WorkDays},{lastresultModel.Data.LateDays},{lastresultModel.Data.EarlyDays});");
                    foreach (var item in lastresultModel.Data.DayVoList)
                    {
                        var flagedate = DateTime.Parse(startDate.AddMonths(1).ToString("yyyy-MM") + "-" + item.Day);
                        dbConnection.Execute($"""
                                              INSERT INTO public.attendancerecordday(untilthisday,day,checkinrule,isnormal,isabnormal,isapply,clockinnumber,workhours,attendancedate,yearmonth)
                                                                                                      VALUES({item.UntilThisDay},{item.Day},'{item.CheckInRule}','{item.IsNormal}','{item.IsAbnormal}','{item.IsApply}',{item.ClockInNumber},{(item.WorkHours == null ? 0 : item.WorkHours)},to_timestamp('{flagedate:yyyy-MM-dd 00:00:00}', 'yyyy-mm-dd hh24:mi:ss'),'{startDate.AddMonths(1):yyyy-MM}');
                                              """);
                    }
                }
            }
        }

        dbConnection.Dispose();
    }
}
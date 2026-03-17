using System.Data;
using System.Text;
using Dapper;
using FreeWim.Models.Attendance;
using FreeWim.Models.Attendance.Dto;
using FreeWim.Models.PmisAndZentao;
using FreeWim.Utils;
using Hangfire;
using Hangfire.Server;
using ModelContextProtocol.Server;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Npgsql;
using System.ComponentModel;

namespace FreeWim.Services;

[McpServerToolType]
public class AttendanceService(
    IConfiguration configuration,
    PushMessageService pushMessageService,
    TokenService tokenService,
    WorkFlowExecutorService workFlowExecutorService,
    PmisService pmisService)
{
    /// <summary>
    /// 获取打卡记录通过外围接口
    /// </summary>
    /// <param name="date"></param>
    /// <param name="sn"></param>
    /// <returns></returns>
    public async Task<List<ZktItem>> GetPunchCardRecordsFromExternalApi(string date, string sn)
    {
        var httpRequestHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;

        var startDateTime = DateTime.Parse(date + " 00:00:00");
        var endDateTime = DateTime.Parse(date + " 23:59:59");

        var response = await httpRequestHelper.PostAsync(
            pmisInfo.ZkUrl + "/api/v2/transaction/get/?key=" + pmisInfo.ZkKey,
            new
            {
                starttime = startDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                endtime = endDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            });

        var result = await response.Content.ReadAsStringAsync();
        var resultModel = JsonConvert.DeserializeObject<ZktResponse>(result);

        // Filter by SN if provided
        var filteredRecords = resultModel?.Data?.Items?.Where(item => item.Sn == sn).ToList();

        return filteredRecords ?? new List<ZktItem>();
    }

    /// <summary>
    /// 根据日期获取当日工时
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public double GetWorkHoursByDate(DateTime date)
    {
        // 这里的 date.Date 确保了即使传入的 date 带有时分秒，也会被重置为零点
        var queryDate = date.Date;
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        string sql = @"
    SELECT workhours 
    FROM public.attendancerecordday 
    WHERE attendancedate = @TargetDate 
      AND workhours > 0 
    LIMIT 1";
        double hours = dbConnection.QueryFirstOrDefault<double>(sql, new { TargetDate = queryDate });
        return hours;
        // var attendanceList = dbConnection.Query<WorkHoursInOutTime>($@"select
        //                                     	clockintype,
        //                                     	max(clockintime) as clockintime
        //                                     from
        //                                     	public.attendancerecorddaydetail
        //                                     where
        //                                     	to_char(attendancedate,'yyyy-MM-dd') = '{date:yyyy-MM-dd}'
        //                                     group by
        //                                     	clockintype").ToList();
        // DateTime? signInDate = null;
        // DateTime? signOutDate = null;
        // if (attendanceList.FirstOrDefault(e => e.ClockInType == 0) != null)
        //     signInDate = attendanceList.FirstOrDefault(e => e.ClockInType == 0)?.ClockInTime;
        // if (attendanceList.FirstOrDefault(e => e.ClockInType == 1) != null)
        //     signOutDate = attendanceList.FirstOrDefault(e => e.ClockInType == 1)?.ClockInTime;
        // if (signInDate == null || signOutDate == null) return hours;
        // if (isSignout.First() == "休息")
        // {
        //     hours = Math.Floor((signOutDate.Value - signInDate.Value).TotalHours * 2) / 2;
        // }
        // else
        // {
        //     signInDate = RoundToHalfHour(signInDate.Value, RoundDirection.Up);
        //     signOutDate = RoundToHalfHour(signOutDate.Value, RoundDirection.Down);
        //     hours = (signOutDate.Value - signInDate.Value).TotalHours;

        //     var noonStart = new DateTime(signInDate.Value.Year, signInDate.Value.Month, signInDate.Value.Day, 12, 0, 0);
        //     var noonEnd = new DateTime(signInDate.Value.Year, signInDate.Value.Month, signInDate.Value.Day, 13, 0, 0);

        //     // 计算时间段与午休时间的重叠
        //     var overlapStart = signInDate > noonStart ? signInDate : noonStart;
        //     var overlapEnd = signOutDate < noonEnd ? signOutDate : noonEnd;

        //     double overlapHours = 0;
        //     if (overlapStart < overlapEnd) overlapHours = (overlapEnd.Value - overlapStart.Value).TotalHours;
        //     hours = hours - overlapHours;
        // }

        // return hours - overlapHours;
        //return hours;
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
                    return new DateTime(dt.AddHours(1).Year, dt.AddHours(1).Month, dt.AddHours(1).Day,
                        dt.AddHours(1).Hour, 0, 0);

            case RoundDirection.Down:
                var roundedMinute = minute < 30 ? 0 : 30;
                return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, roundedMinute, 0);

            case RoundDirection.Nearest:
                if (minute < 15)
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);
                else if (minute < 45)
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 30, 0);
                else
                    return new DateTime(dt.AddHours(1).Year, dt.AddHours(1).Month, dt.AddHours(1).Day,
                        dt.AddHours(1).Hour, 0, 0);

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
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var jobId = context?.BackgroundJob.Id;
        if (!string.IsNullOrEmpty(jobId))
        {
            var autoCheckInRecord = dbConnection
                .Query<AutoCheckInRecord>($@"SELECT * FROM public.autocheckinrecord WHERE jobid = '{jobId}'")
                .FirstOrDefault();
            if (autoCheckInRecord != null)
            {
                var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
                var url = $"{pmisInfo.ZkUrl}/iclock/cdata?SN={pmisInfo.ZkSN}&table=ATTLOG&Stamp=9999";
                var contentString =
                    $"100{pmisInfo.UserAccount}\t{autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss}\t0\t15\t0\t0\t0";
                using var client = new HttpClient();
                var content = new StringContent(contentString, Encoding.UTF8, "text/plain");
                var response = client.PostAsync(url, content).Result;
                var result = response.Content.ReadAsStringAsync().Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (result.Contains("OK:1"))
                    {
                        dbConnection.Execute(
                            $@"UPDATE public.autocheckinrecord SET clockinstate = 1,updateat = now() WHERE jobid = '{jobId}'");
                        pushMessageService.Push("任务调度",
                            $"您设定于 {autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss} 执行的任务已执行，请关注后续考勤同步信息。",
                            PushMessageService.PushIcon.Zktime);
                        if (autoCheckInRecord.clockintime.Hour <= 10) return;
                        if (!string.IsNullOrEmpty(pmisInfo.ShutDownUrl))
                        {
                            try
                            {
                                using var ShutDownClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                                var shutDownUrl = pmisInfo.ShutDownUrl;
                                var shutDownResponse = ShutDownClient.GetAsync(shutDownUrl).Result;
                                if (shutDownResponse.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    pushMessageService.Push("关机提醒", $"您的电脑即将关机", PushMessageService.PushIcon.Close);
                                }
                            }
                            catch (Exception ex)
                            {
                                // 忽略关机接口调用失败，可能是机器已关机
                                Console.WriteLine($"调用关机接口失败: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        dbConnection.Execute(
                            $@"UPDATE public.autocheckinrecord SET clockinstate = 2,updateat = now() WHERE jobid = '{jobId}'");
                        pushMessageService.Push("任务调度",
                            $"您设定于 {autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss} 执行的任务未能成功完成。\n失败原因：" + result,
                            PushMessageService.PushIcon.Alert);
                    }
                }
                else
                {
                    dbConnection.Execute(
                        $@"UPDATE public.autocheckinrecord SET clockinstate = 2 ,updateat = now() WHERE jobid = '{jobId}'");
                    pushMessageService.Push("任务调度",
                        $"您设定于 {autoCheckInRecord.clockintime:yyyy-MM-dd HH:mm:ss} 执行的任务未能成功完成。\n接口调用失败：" + result,
                        PushMessageService.PushIcon.Alert);
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
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var lastDay = dbConnection.Query<string>($@"select
                                                                                                	checkinrule
                                                                                                from
                                                                                                	public.attendancerecordday
                                                                                                where
                                                                                                	to_char(attendancedate,
                                                                                                	'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}'")
            .FirstOrDefault();
        if (lastDay == null) return;
        if (lastDay != "休息") return;
        var pushMessage = "";
        var listOfPersonnel = configuration.GetSection("ListOfPersonnel").Get<List<ListOfPersonnel>>();
        if (listOfPersonnel != null)
        {
            var response = httpRequestHelper.PostAsync(
                pmisInfo.ZkUrl + "/api/v2/transaction/get/?key=" + pmisInfo.ZkKey,
                new
                {
                    starttime = DateTime.Now.ToString("yyyy-MM-dd") + " 00:00:00",
                    endtime = DateTime.Now.ToString("yyyy-MM-dd") + " 23:59:59",
                    sn = pmisInfo.ZkSN
                }).Result;
            var result = response.Content.ReadAsStringAsync().Result;
            var resultModel = JsonConvert.DeserializeObject<ZktResponse>(result);
            if (resultModel?.Data is { Count: > 0 })
            {
                // 1. 基础过滤与非空校验
                var personList = resultModel.Data.Items?
                    .Where(e => e is { Alias: "郑州", Deptname: "郑州驻外办" } && !string.IsNullOrEmpty(e.Checktime))
                    .ToList() ?? new List<ZktItem>();

                if (personList.Count == 0) return;

                // 2. 一次性从数据库查询当天已存在的打卡记录 (避免循环查询)
                // 假设 clockintime 是字符串或日期，通过参数化查询提高安全性
                var existingRecords = dbConnection.Query<(string Name, string ClockTime)>(
                    $"SELECT name, to_char(clockintime, 'yyyy-mm-dd hh24:mi:ss') FROM public.checkinwarning WHERE to_char(clockintime, 'yyyy-mm-dd') = '{DateTime.Now:yyyy-MM-dd}'"
                ).ToHashSet();

                // 3. 计算差集：找出在 personList 中但不在数据库中的记录
                var newRecords = personList
                    .Where(p => !existingRecords.Contains((p.Ename, p.Checktime)))
                    .ToList();

                if (newRecords.Count == 0) return;

                // 4. 准备批量插入的数据和推送消息
                var insertList = new List<object>();
                var sbMessage = new StringBuilder(pushMessage);

                foreach (var person in newRecords)
                {
                    // 查找人员花名
                    var staff = listOfPersonnel.FirstOrDefault(e => e.RealName == person.Ename);
                    if (staff != null)
                    {
                        var timeStr = DateTime.Parse(person.Checktime).ToString("HH:mm:ss");
                        sbMessage.Append($"{staff.FlowerName}-打卡时间:{timeStr}\n");
                    }

                    // 准备插入对象
                    insertList.Add(new
                    {
                        Id = Guid.NewGuid(),
                        Name = person.Ename,
                        Clockintime = DateTime.Parse(person.Checktime)
                    });
                }

                // 5. 批量执行插入 (Dapper 支持传入集合进行批量操作)
                if (insertList.Count > 0)
                {
                    dbConnection.Execute(
                        "INSERT INTO public.checkinwarning(id, name, clockintime) VALUES(@Id, @Name, @Clockintime)",
                        insertList
                    );
                }

                pushMessage = sbMessage.ToString();
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
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", tokenService.GetTokenAsync());
        var startDate = DateTime.Now;
        if (DateTime.Now.Hour <= 7 || DateTime.Now.Hour >= 23) return;

        var response = client.GetAsync(pmisInfo!.Url + "/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" +
                                       startDate.ToString("yyyy-MM")).Result;
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
                                                                      e.ClockInType ==
                                                                      int.Parse(daydetailitem.ClockInType) &&
                                                                      e.ClockInTime ==
                                                                      (DateTime.TryParse(daydetailitem.ClockInTime,
                                                                          out var parsedDate)
                                                                          ? parsedDate
                                                                          : null)))
                                    {
                                        insertIdent = true;
                                        if (string.IsNullOrEmpty(daydetailitem.ClockInTime)) continue;
                                        pushMessage = "数据已同步\n" +
                                                      (daydetailitem.ClockInType != null &&
                                                       int.Parse(daydetailitem.ClockInType) == 0
                                                          ? "签到时间:"
                                                          : "签退时间:") + daydetailitem.ClockInTime;
                                        if (daydetailitem.ClockInType != null &&
                                            int.Parse(daydetailitem.ClockInType) == 1) signout = true;
                                    }

            if (insertIdent)
            {
                dbConnection.Execute(
                    $"delete from public.attendancerecord where attendancemonth = '{startDate:yyyy-MM}'");
                dbConnection.Execute(
                    $"delete from public.attendancerecordday where to_char(attendancedate,'yyyy-mm') = '{startDate:yyyy-MM}'");
                dbConnection.Execute(
                    $"delete from public.attendancerecorddaydetail where to_char(attendancedate,'yyyy-mm') = '{startDate:yyyy-MM}'");
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

                if (!string.IsNullOrEmpty(pushMessage))
                    pushMessageService.Push("考勤", pushMessage, PushMessageService.PushIcon.Attendance);
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
            var lastMonthData = dbConnection
                .Query<int>(
                    $@"select count(0) from public.attendancerecordday where yearmonth = '{startDate.AddMonths(1):yyyy-MM}'")
                .First();
            if (lastMonthData == 0)
            {
                var lastresponse = client.GetAsync(pmisInfo!.Url +
                                                   "/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" +
                                                   startDate.AddMonths(1).ToString("yyyy-MM")).Result;
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

    /// <summary>
    /// 获取用户打卡详情
    /// </summary>
    /// <param name="input">查询条件</param>
    /// <returns>打卡详情列表</returns>
    [McpServerTool(Name = "GetUserClockInDetails")]
    [Description("获取用户打卡详情")]
    public List<UserClockInDetailsOutput> GetUserClockInDetails(UserClockInDetailsInput input)
    {
        using IDbConnection dbConnection = new MySqlConnection(configuration["OAConnection"]);

        string sql = @"
            SELECT
                id as Id,
                user_id as UserId,
                user_name as UserName,
                org_name as OrgName,
                DATE_FORMAT(clock_in_date, '%Y-%m-%d') as ClockInDate,
                day_of_week as DayOfWeek,
                check_in_rule as CheckInRule,
                work_hours as WorkHours,
                work_minutes as WorkMinutes,
                clock_in_number as ClockInNumber,
                is_late as IsLate,
                is_early as IsEarly,
                is_absenteeism as IsAbsenteeism,
                is_rest as IsRest,
                is_out as IsOut,
                work_overtime as WorkOvertime,
                leave_hours as LeaveHours,
                leave_type as LeaveType
            FROM hd_oa.oa_user_clock_in_record
            WHERE clock_in_date BETWEEN @starttime AND @endtime
            AND (@userid IS NULL OR user_id = @userid)
            AND (@username IS NULL OR user_name = @username)
            ORDER BY clock_in_date DESC";

        return dbConnection.Query<UserClockInDetailsOutput>(sql, new
        {
            starttime = input.StartTime,
            endtime = input.EndTime,
            userid = input.UserId,
            username = input.UserName
        }).ToList();
    }

    /// <summary>
    /// 获取最新考勤统计数据
    /// </summary>
    /// <returns>工作日、工时、日均工时</returns>
    public object GetLatestAttendanceStats()
    {
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var workDays = dbConnection
            .Query<int>(
                "select count(0) from (select to_char(attendancedate,'yyyy-mm-dd'),count(0) from public.attendancerecorddaydetail  group by to_char(attendancedate,'yyyy-mm-dd'))")
            .First();
        var workHours = dbConnection.Query<decimal>("select sum(workhours) from public.attendancerecordday").First();

        return new
        {
            WorkDays = workDays,
            WorkHours = workHours,
            DayAvg = Math.Round((double)workHours / workDays, 2)
        };
    }

    /// <summary>
    /// 获取日历数据
    /// </summary>
    /// <param name="start">开始日期</param>
    /// <param name="end">结束日期</param>
    /// <returns>日历数据列表</returns>
    public List<AttendanceCalendarOutput> GetCalendarData(string start = "", string end = "")
    {
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var sqlwhere = " where 1=1 ";
        if (!string.IsNullOrEmpty(start)) sqlwhere += $" and a.clockintime >= '{DateTime.Parse(start)}'";
        if (!string.IsNullOrEmpty(end))
            sqlwhere += $" and a.clockintime <= '{DateTime.Parse(end).AddDays(1).AddSeconds(-1)}'";

        var workList = dbConnection.Query<AttendanceCalendarOutput>(@"select
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
        return workList;
    }

    /// <summary>
    /// 取消加班
    /// </summary>
    /// <param name="input">输入参数</param>
    /// <returns>受影响行数</returns>
    [McpServerTool(Name = "CancelOverTimeWork")]
    [Description("取消加班申请")]
    public int CancelOverTimeWork()
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
        return rowsCount;
    }

    /// <summary>
    /// 恢复加班
    /// </summary>
    /// <param name="input">输入参数</param>
    /// <returns>恢复结果</returns>
    [McpServerTool(Name = "RestoreOverTimeWork")]
    [Description("恢复加班申请")]
    public object RestoreOverTimeWork()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var rowsCount = dbConnection.Execute($@"DELETE FROM
                                                public.overtimerecord
                                            WHERE work_date = '{DateTime.Now:yyyy-MM-dd}';");

        return new
        {
            rowsCount,
            pmisInfo.OverStartTime,
            pmisInfo.OverEndTime,
            TotalHours = (DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverEndTime) -
                          DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + pmisInfo.OverStartTime))
                .TotalHours
                .ToString() + "h"
        };
    }

    /// <summary>
    /// 取消自动打卡
    /// </summary>
    /// <param name="input">输入参数</param>
    /// <returns>是否成功</returns>
    [McpServerTool(Name = "CancelAutoCheckIn")]
    [Description("取消自动打卡，需要通过GetAutoCheckInList获取jobID")]
    public bool CancelAutoCheckIn(AutoCheckInInput input)
    {
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var flag = BackgroundJob.Delete(input.jobId);
        dbConnection.Execute($@"DELETE FROM public.autocheckinrecord WHERE jobid = '{input.jobId}'");
        return flag;
    }

    /// <summary>
    /// 获取自动打卡列表
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="rows">每页行数</param>
    /// <returns>自动打卡记录列表</returns>
    [McpServerTool(Name = "GetAutoCheckInList")]
    [Description("获取自动打卡任务列表")]
    public List<AutoCheckInRecord> GetAutoCheckInList(int page = 1, int rows = 10)
    {
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var offset = (page - 1) * rows;
        return dbConnection
            .Query<AutoCheckInRecord>(
                $@"SELECT * FROM public.autocheckinrecord ORDER BY clockintime desc LIMIT :rows OFFSET :offset",
                new { rows, offset }).ToList();
    }

    /// <summary>
    /// 获取周末加班数据
    /// </summary>
    /// <param name="input">查询条件</param>
    /// <returns>周末加班数据列表</returns>
    public List<WorkingOvertimeOnWeekendsIOutput> GetWorkingOvertimeOnWeekends(WorkingOvertimeOnWeekendsInput input)
    {
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var offset = (input.Page - 1) * input.Rows;
        return dbConnection.Query<WorkingOvertimeOnWeekendsIOutput>($@"SELECT
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
                input.Order,
                rows = input.Rows,
                offset = offset,
                sort = input.Sort,
                starttime = input.StartTime + " 00:00:00",
                endtime = input.EndTime + " 23:59:59"
            }).ToList();
    }

    /// <summary>
    /// 获取工时排名
    /// </summary>
    /// <param name="input">查询条件</param>
    /// <returns>工时排名列表</returns>
    [McpServerTool(Name = "GetRankingOfWorkingHours")]
    [Description("获取工时排名")]
    public List<RankingOfWorkingHoursOutput> GetRankingOfWorkingHours(RankingOfWorkingHoursInput input)
    {
        using IDbConnection dbConnection = new MySqlConnection(configuration["OAConnection"]);
        var offset = (input.Page - 1) * input.Rows;
        return dbConnection.Query<RankingOfWorkingHoursOutput>($@"WITH base_summary AS (
    SELECT
        user_name,
        user_id,
        GROUP_CONCAT(DISTINCT org_name) as org_name,
        SUM(work_hours) as total_work_hours,
        SUM(work_overtime) as raw_overtime_sum,
        SUM(work_overtime / 60.0) as total_overtime
    FROM hd_oa.oa_user_clock_in_record
    WHERE clock_in_date BETWEEN @starttime AND @endtime
    {(string.IsNullOrEmpty(input.OrgName) ? "" : " AND org_name LIKE @orgname ")}
    {(string.IsNullOrEmpty(input.UserName) ? "" : " AND user_name LIKE @username ")}
    GROUP BY user_id, user_name
),
ranked_stats AS (
    SELECT
        *,
        RANK() OVER (ORDER BY total_work_hours DESC) as work_rank,
        RANK() OVER (ORDER BY raw_overtime_sum DESC) as overtime_rank,
        COUNT(*) OVER () as total_users
    FROM base_summary
)
SELECT
    user_name as UserName,
    user_id as UserId,
    org_name as OrgName,
    total_work_hours as TotalWorkHours,
    total_overtime as TotalOvertime,
    work_rank as WorkRank,
    (total_users - work_rank) as WorkSurpassedCount,
    ROUND((total_users - work_rank) * 100.0 / GREATEST(total_users - 1, 1), 2) as WorkSurpassedPercent,
    overtime_rank as OvertimeRank,
    (total_users - overtime_rank) as OvertimeSurpassedCount,
    ROUND((total_users - overtime_rank) * 100.0 / GREATEST(total_users - 1, 1), 2) as OvertimeSurpassedPercent,
    CONCAT('工时排名第', work_rank, '名，超越了', total_users - work_rank, '人(',
           ROUND((total_users - work_rank) * 100.0 / GREATEST(total_users - 1, 1), 2), '%)；',
           '加班排名第', overtime_rank, '名，超越了', total_users - overtime_rank, '人(',
           ROUND((total_users - overtime_rank) * 100.0 / GREATEST(total_users - 1, 1), 2), '%)') as RankDescription
FROM ranked_stats ORDER BY {input.Order} {input.Sort} LIMIT {input.Rows} OFFSET {offset};",
            new
            {
                starttime = input.StartTime,
                endtime = input.EndTime,
                orgname = $"%{input.OrgName}%",
                username = $"%{input.UserName}%"
            }).ToList();
    }

    /// <summary>
    /// 获取高级工时统计
    /// </summary>
    /// <param name="input">查询条件</param>
    /// <returns>高级工时统计列表</returns>
    [McpServerTool(Name = "GetAdvancedWorkHoursStatistics")]
    [Description("获取高级工时统计")]
    public List<AdvancedWorkHoursStatisticsOutput> GetAdvancedWorkHoursStatistics(RankingOfWorkingHoursInput input)
    {
        using IDbConnection dbConnection = new MySqlConnection(configuration["OAConnection"]);
        var offset = (input.Page - 1) * input.Rows;

        string mappedOrderField = input.Order?.ToLower() switch
        {
            "user_name" or "username" => "r.username",
            "org_name" or "orgname" => "r.orgname",
            "out_days" or "outdays" => "outdays",
            "offset_leave_days" or "offsetleavedays" => "offsetleavedays",
            "general_leave_days" or "generalleavedays" => "generalleavedays",
            "base_working_days" or "baseworkingdays" => "baseworkingdays",
            "actual_attendance_days" or "actualattendancedays" => "actualattendancedays",
            "delay_overtime_hours" or "delayovertimehours" => "delayovertimehours",
            "weekend_overtime_hours" or "weekendovertimehours" => "weekendovertimehours",
            "total_overtime_hours" or "totalovertimehours" => "totalovertimehours",
            "total_actual_work_hours" or "totalactualworkhours" or "total_work_hours" => "totalactualworkhours",
            "daily_avg_work_hours" or "dailyavgworkhours" => "dailyavgworkhours",
            _ => "totalactualworkhours"
        };

        return dbConnection.Query<AdvancedWorkHoursStatisticsOutput>($@"
WITH RECURSIVE calendar AS (
    SELECT CAST(@starttime AS DATE) AS t_date
    UNION ALL
    SELECT DATE_ADD(t_date, INTERVAL 1 DAY)
    FROM calendar
    WHERE t_date < @endtime
),
working_days_count AS (
    SELECT COUNT(*) AS total_working_days
    FROM calendar c
    LEFT JOIN hd_oa.oa_holiday h ON c.t_date = h.date
    WHERE (h.holiday = 0) OR (h.date IS NULL AND WEEKDAY(c.t_date) < 5)
),
user_overtime AS (
    SELECT
        user_id,
        SUM(CASE WHEN work_overtime_type = '1' THEN CAST(IFNULL(realtime, 0) AS DECIMAL(10,2)) ELSE 0 END) AS delay_overtime_hours,
        SUM(CASE WHEN work_overtime_type != '1' THEN CAST(IFNULL(realtime, 0) AS DECIMAL(10,2)) ELSE 0 END) AS weekend_overtime_hours
    FROM
        oa_work_overtime
    WHERE
        is_pass = '1'
        AND hddev_proc_status = 'COMPLETED'
        AND work_date BETWEEN @starttime AND @endtime
    GROUP BY user_id
)

SELECT
    r.user_id AS UserId,
    r.user_name AS UserName,
    r.org_name AS OrgName,
    r.sn AS UserSn,
    w.total_working_days AS BaseWorkingDays,
    COUNT(CASE WHEN r.is_out = '1' THEN 1 END) AS OutDays,
    ROUND(SUM(CASE WHEN r.leave_type = '1' THEN r.leave_hours ELSE 0 END) / 7.5, 2) AS OffsetLeaveDays,
    ROUND(SUM(CASE WHEN r.leave_type IN ('2','3','4','5','6','7','8','9','14') THEN r.leave_hours ELSE 0 END) / 7.5, 2) AS GeneralLeaveDays,
    (w.total_working_days
     - ROUND(SUM(CASE WHEN r.leave_type IN ('1','2','3','4','5','6','7','8','9','14') THEN r.leave_hours ELSE 0 END) / 7.5, 2)
    ) AS ActualAttendanceDays,
    IFNULL(ot.delay_overtime_hours, 0) AS DelayOvertimeHours,
    IFNULL(ot.weekend_overtime_hours, 0) AS WeekendOvertimeHours,
    (IFNULL(ot.delay_overtime_hours, 0) + IFNULL(ot.weekend_overtime_hours, 0)) AS TotalOvertimeHours,
    ROUND(
        ((w.total_working_days
          - (SUM(CASE WHEN r.leave_type IN ('1','2','3','4','5','6','7','8','9','14') THEN r.leave_hours ELSE 0 END) / 7.5)
        ) * 7.5)
        + (IFNULL(ot.delay_overtime_hours, 0) + IFNULL(ot.weekend_overtime_hours, 0)), 1
    ) AS TotalActualWorkHours,
    ROUND(
        (
            ((w.total_working_days
              - (SUM(CASE WHEN r.leave_type IN ('1','2','3','4','5','6','7','8','9','14') THEN r.leave_hours ELSE 0 END) / 7.5)
            ) * 7.5)
            + (IFNULL(ot.delay_overtime_hours, 0) + IFNULL(ot.weekend_overtime_hours, 0))
        ) / w.total_working_days, 1
    ) AS DailyAvgWorkHours

FROM
    oa_user_clock_in_record r
CROSS JOIN
    working_days_count w
LEFT JOIN
    user_overtime ot ON r.user_id = ot.user_id
WHERE
    r.clock_in_date BETWEEN @starttime AND @endtime
    {(string.IsNullOrEmpty(input.OrgName) ? "" : " AND r.org_name LIKE @orgname ")}
    {(string.IsNullOrEmpty(input.UserName) ? "" : " AND r.user_name LIKE @username ")}
GROUP BY
    r.user_id,
    r.user_name,
    r.sn,
    w.total_working_days,
    ot.delay_overtime_hours,
    ot.weekend_overtime_hours
ORDER BY {mappedOrderField} {input.Sort} LIMIT {input.Rows} OFFSET {offset};",
            new
            {
                starttime = input.StartTime,
                endtime = input.EndTime,
                orgname = $"%{input.OrgName}%",
                username = $"%{input.UserName}%"
            }).ToList();
    }

    /// <summary>
    /// 获取考勤异常汇总
    /// </summary>
    /// <param name="input">查询条件</param>
    /// <returns>考勤异常汇总列表</returns>
    [McpServerTool(Name = "GetAttendanceAbnormalSummary")]
    [Description("获取考勤异常汇总")]
    public List<AttendanceAbnormalSummaryOutput> GetAttendanceAbnormalSummary(AttendanceAbnormalSummaryInput input)
    {
        using IDbConnection dbConnection = new MySqlConnection(configuration["OAConnection"]);
        var offset = (input.Page - 1) * input.Rows;

        var validOrderFields = new[]
        {
            "UserId", "UserName", "OrgName", "MissingCardDays", "EarlyLeaveDays",
            "LateAndEarlyLeaveDays", "FieldLateDays", "FieldEarlyLeaveDays", "SupplementCardDays",
            "CompensatoryLeaveDays", "TotalLeaveDays", "TotalAbnormalDays"
        };
        var orderBy = validOrderFields.Contains(input.Order ?? "") ? input.Order : "TotalAbnormalDays";
        var sortOrder = (input.Sort?.ToLower() == "asc") ? "ASC" : "DESC";

        return dbConnection.Query<AttendanceAbnormalSummaryOutput>($@"SELECT
                r.user_id AS UserId,
                r.user_name AS UserName,
                r.org_id AS OrgId,
                r.org_name AS OrgName,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '缺卡' THEN r.clock_in_date END) AS MissingCardDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '早退' THEN r.clock_in_date END) AS EarlyLeaveDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '迟到' THEN r.clock_in_date END) AS LateAndEarlyLeaveDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '外勤/迟到' THEN r.clock_in_date END) AS FieldLateDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '外勤/早退' THEN r.clock_in_date END) AS FieldEarlyLeaveDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '补卡' THEN r.clock_in_date END) AS SupplementCardDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name = '调休' THEN r.clock_in_date END) AS CompensatoryLeaveDays,
                COUNT(DISTINCT CASE WHEN t.clock_in_status_name IN ('事假','病假','婚假','丧假','产假','产检假','陪产假','哺乳假','路程假') THEN r.clock_in_date END) AS TotalLeaveDays,
                COUNT(DISTINCT r.clock_in_date) AS TotalAbnormalDays
            FROM
                oa_user_clock_in_record r
            JOIN
                oa_user_clock_in_record_time t ON r.id = t.record_id
            WHERE
                r.clock_in_date BETWEEN @starttime AND @endtime
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
            }).ToList();
    }

    /// <summary>
    /// 获取考勤异常明细
    /// </summary>
    /// <param name="input">查询条件</param>
    /// <returns>考勤异常明细列表</returns>
    [McpServerTool(Name = "GetAttendanceAbnormalDetail")]
    [Description("获取考勤异常明细")]
    public List<AttendanceAbnormalDetailOutput> GetAttendanceAbnormalDetail(AttendanceAbnormalDetailInput input)
    {
        using IDbConnection dbConnection = new MySqlConnection(configuration["OAConnection"]);
        var offset = (input.Page - 1) * input.Rows;

        var validOrderFields = new[] { "ClockInDate", "UserName", "TotalAbnormalMinutes" };
        var orderBy = validOrderFields.Contains(input.Order ?? "") ? input.Order : "ClockInDate";
        var sortOrder = (input.Sort?.ToLower() == "asc") ? "ASC" : "DESC";

        return dbConnection.Query<AttendanceAbnormalDetailOutput>($@"SELECT
                r.clock_in_date AS ClockInDate,
                r.user_name AS UserName,
                GROUP_CONCAT(DATE_FORMAT(t.clock_in_time, '%H:%i:%s') ORDER BY t.clock_in_time SEPARATOR ' | ') AS ActualClockInTime,
                GROUP_CONCAT(t.clock_in_status_name ORDER BY t.clock_in_time SEPARATOR ' | ') AS AbnormalStatus,
                SUM(t.later_early_minutes) AS TotalAbnormalMinutes,
                GROUP_CONCAT(t.remark SEPARATOR '; ') AS RemarkSummary
            FROM
                oa_user_clock_in_record r
            JOIN
                oa_user_clock_in_record_time t ON r.id = t.record_id
            WHERE
                r.clock_in_date BETWEEN @starttime AND @endtime
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
            }).ToList();
    }

    /// <summary>
    /// 获取考勤面板数据
    /// </summary>
    /// <returns>考勤面板数据</returns>
    public object GetBoardData()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);

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

        var thisMonthData = dbConnection.Query<(int Day, double Workhours)>(sqlThisMonth).ToList();
        var lastMonthData = dbConnection.Query<(int Day, double Workhours)>(sqlLastMonth).ToList();

        var sql = @"
        SELECT
            TO_CHAR(attendancedate, 'YYYY-MM') AS Month,
            SUM(workhours) AS TotalHours,
            SUM(CASE when checkinrule <> '休息' AND workhours > 7.5 THEN workhours - 7.5 WHEN checkinrule = '休息' then workhours ELSE 0 END) AS OvertimeHours
        FROM attendancerecordday
        WHERE attendancedate >= date_trunc('month', CURRENT_DATE) - INTERVAL '5 months' and attendancedate < date_trunc('month', CURRENT_DATE) + INTERVAL '1 months'
        GROUP BY TO_CHAR(attendancedate, 'YYYY-MM')
        ORDER BY Month;";

        var result = dbConnection.Query<(string Month, double TotalHours, double OvertimeHours)>(sql);

        var sqlCheckIn = @"SELECT
    TO_CHAR(attendancedate::date, 'MM.DD') AS DayLabel,
    TO_CHAR(MIN(clockintime), 'HH24:MI') AS CheckInTime
FROM attendancerecorddaydetail
WHERE clockintype = '0'
  AND attendancedate >= CURRENT_DATE - INTERVAL '30 days'
  AND attendancedate < CURRENT_DATE + INTERVAL '1 day'
GROUP BY attendancedate::date
ORDER BY attendancedate::date";

        var sqlCheckOut = @"
        SELECT
            TO_CHAR(attendancedate::date, 'MM.DD') AS DayLabel,
            TO_CHAR(MAX(clockintime), 'HH24:MI') AS CheckOutTime
        FROM attendancerecorddaydetail
        WHERE clockintype = '1'
          AND attendancedate >= CURRENT_DATE - INTERVAL '30 days'
          AND attendancedate < CURRENT_DATE + INTERVAL '1 day'
        GROUP BY attendancedate::date
        ORDER BY attendancedate::date;";

        var checkIns = dbConnection.Query<(string DayLabel, string CheckInTime)>(sqlCheckIn).ToList();
        var checkOuts = dbConnection.Query<(string DayLabel, string CheckOutTime)>(sqlCheckOut).ToList();

        var punchHeatsql = @"
                        SELECT
                          TO_CHAR(attendancedate::date, 'YYYY-MM-DD') AS Date,
                          COALESCE(SUM(workhours), 0) AS Total
                        FROM attendancerecordday
                        WHERE attendancedate >= CURRENT_DATE - INTERVAL '1 year'
                        GROUP BY attendancedate::date
                        ORDER BY attendancedate::date";

        var punchHeatresult = dbConnection.Query<(string Date, decimal Total)>(punchHeatsql);
        var punchHeatmap = punchHeatresult.Select(r => new object[] { r.Date, (double)r.Total }).ToList();

        var sqlforHeader = @"SELECT
    (SELECT SUM(workhours)
     FROM attendancerecordday
     WHERE yearmonth = to_char(current_date, 'YYYY-MM')) AS this_month,
    (SELECT SUM(workhours)
     FROM attendancerecordday
     WHERE yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM')) AS last_month,
    (SELECT SUM(workhours)
     FROM attendancerecordday
     WHERE yearmonth = to_char(current_date - interval '2 month', 'YYYY-MM')) AS before_last_month,
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
    END AS last_vs_before_last_percent;";

        var HeaderResult =
            dbConnection
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
            dbConnection.Query<(double thisavghours, double lastavghours, double improvepercent)>(sqlforavgworkhours);

        var sqlforavgovertime = @"select
	round(this_avg.overtimeday::numeric / nullif(this_avg.workdays, 0) * 100, 2) as thisavghours,
	round(last_avg.overtimeday::numeric / nullif(last_avg.workdays, 0) * 100, 2) as lastavghours,
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
		sum(case when workhours >= 8.5 then 1 else 0 end) as overtimeday,
		sum(case when checkinrule <> '休息' then 1 else 0 end) as workdays
	from
		attendancerecordday
	where
		yearmonth = to_char(current_date, 'YYYY-MM')
		and to_char(attendancedate,'yyyy-MM-dd') < to_char(now(),'yyyy-MM-dd')
) as this_avg,
	(
	select
		sum(case when workhours >= 8.5 then 1 else 0 end) as overtimeday,
		sum(case when checkinrule <> '休息' then 1 else 0 end) as workdays
	from
		attendancerecordday
	where
		yearmonth = to_char(current_date - interval '1 month', 'YYYY-MM')
) as last_avg;";

        var avgovertimeResult =
            dbConnection.Query<(double thisavghours, double lastavghours, double improvepercent)>(sqlforavgovertime);

        var sqlforovertimerecord = @"select
	id,
	work_date as date,
	contract_unit as contractunit,
	coalesce ( to_char(plan_start_time , 'HH24:MI'),
	'') as start,
	coalesce (to_char(plan_end_time , 'HH24:MI') ,
	'') as end,
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

        var overtimerecord = dbConnection
            .Query<(string id, string date, string contractunit, string start, string end, string duration, string
                status)>(sqlforovertimerecord).ToList();

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

        return new
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
    }

    /// <summary>
    /// 创建自动打卡计划
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [McpServerTool(Name = "CreateAutoCheckIn")]
    [Description("创建自动打卡计划")]
    public object CreateAutoCheckIn(AutoCheckInInput input)
    {
        if (input.SelectTime != null)
        {
            if (input.SelectTime < DateTime.Now)
                return new { jobId = "", SelectTime = input.SelectTime, message = "登记失败,时间不能小于当前时间" };

            // 早上9点之前立即执行
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
                return new { jobId = "", SelectTime = input.SelectTime, message = "登记失败,今日操作过于频繁" };

            var jobId = BackgroundJob.Schedule(() => AutoCheckIniclock(null), input.SelectTime.Value);
            dbConnection.Execute(
                $@"insert
                	into
                	public.autocheckinrecord(id, jobid, clockintime, clockinstate)
                values('{Guid.NewGuid().ToString()}', '{jobId}', to_timestamp('{input.SelectTime:yyyy-MM-dd HH:mm:ss}', 'yyyy-mm-dd hh24:mi:ss'), 0)");
            dbConnection.Dispose();
            return new { jobId = jobId, SelectTime = input.SelectTime, message = "成功" };
        }
        else
        {
            return new { jobId = "", SelectTime = input.SelectTime, message = "登记失败,请选择时间" };
        }
    }
}
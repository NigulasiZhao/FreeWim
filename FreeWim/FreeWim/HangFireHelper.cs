using Dapper;
using Newtonsoft.Json;
using Npgsql;
using FreeWim.Models.Attendance;
using FreeWim.Models.EventInfo;
using System.Data;
using System.Text;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using FreeWim.Common;
using FreeWim.Models.PmisAndZentao;

namespace FreeWim;

public class HangFireHelper(
    IConfiguration configuration,
    PushMessageHelper pushMessageHelper,
    AttendanceHelper attendanceHelper,
    PmisHelper pmisHelper,
    ZentaoHelper zentaoHelper,
    TokenService tokenService,
    IChatClient chatClient,
    WorkFlowExecutor workFlowExecutor)
{
    public void StartHangFireTask()
    {
        //每日零点0 0 0 */1 * ?
        //每小时0 0 * * * ?
        //每五分钟0 0/5 * * * ?
        //RecurringJob.AddOrUpdate("SpeedTest", () => SpeedTest(), "0 0 */1 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        // 
        using (var connection = JobStorage.Current.GetConnection())
        {
            var recurringJobs = connection.GetRecurringJobs();

            foreach (var job in recurringJobs) RecurringJob.RemoveIfExists(job.Id);
        }

        RecurringJob.AddOrUpdate("考勤同步", () => AttendanceRecord(), "5,35 * * * *", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("Keep数据同步", () => KeepRecord(), "0 0 */3 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("高危人员打卡预警", () => CheckInWarning(), "5,35 * * * *", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("同步禅道任务", () => SynchronizationZentaoTask(), "0 15,17,19 * * *", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("执行禅道完成任务、日报、周报发送", () => ExecuteAllWork(), "0 0/40 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("自动加班申请", () => CommitOvertimeWork(), "0 0/30 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("禅道衡量目标、计划完成成果、实际从事工作与成果信息补全", () => TaskDescriptionComplete(), "0 0/30 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("DeepSeek余额预警", () => DeepSeekBalance(), "0 0 */2 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("提交所有待处理实际加班申请", () => RealOverTime(), "0 0 9 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
    }

    //private static readonly MemoryCache Cache = new(new MemoryCacheOptions());

    public void SpeedTest()
    {
        try
        {
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var speedTestHelper = new SpeedTestHelper();
            var speedResult = speedTestHelper.StartSpeedTest();
            dbConnection.Execute($"""
                                  INSERT INTO speedrecord
                                                                                              (id,
                                                                                              ping,
                                                                                              download, 
                                                                                              upload, 
                                                                                              server_id, 
                                                                                              server_host, 
                                                                                              server_name, 
                                                                                              url, 
                                                                                              scheduled, 
                                                                                              failed)
                                                                                              VALUES('{Guid.NewGuid().ToString()}',
                                                                                                     '{speedResult.Latency}', 
                                                                                                     {speedResult.downloadSpeed},
                                                                                                     {speedResult.uploadSpeed}, 
                                                                                                     {speedResult.Id},
                                                                                                     '{speedResult.Host}',
                                                                                                     '{speedResult.Name}', 
                                                                                                     '{speedResult.Url}',
                                                                                                     0, 
                                                                                                     0)
                                  """);
            //if (!string.IsNullOrEmpty(configuration["PushMessageUrl"])) PushMessage(speedResult);
            dbConnection.Dispose();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    /// <summary>
    /// 考勤数据同步
    /// 每小时的5分，35分调用一次查询当月考勤数据接口；接口结果对比本地库中数据，如果接口返回有新数据，则更新本地数据库
    /// 考勤获取到当日签退记录后，则执行禅道关闭工单，日报发送，周报发送任务
    /// </summary>
    public void AttendanceRecord()
    {
        var signout = false;
        var pushMessage = "";
        var insertIdent = false;
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", tokenService.GetTokenAsync());
        var startDate = DateTime.Now;
        var response = client.GetAsync(pmisInfo!.Url + "/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + startDate.ToString("yyyy-MM")).Result;
        var result = response.Content.ReadAsStringAsync().Result;
        var resultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
        if (resultModel is { Code: 200 })
        {
            //查询已有的今日打卡记录
            var todayAttendanceList = dbConnection.Query<WorkHoursInOutTime>($@"select
                                            	clockintype,
                                            	max(clockintime) as clockintime
                                            from
                                            	public.attendancerecorddaydetail
                                            where
                                            	to_char(attendancedate,'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}'
                                            group by
                                            	clockintype").ToList();
            if (resultModel.Data.DayVoList.Count > 0)
                foreach (var daydetail in resultModel.Data.DayVoList.Where(e => e.Day == DateTime.Today.Day))
                    if (daydetail.DetailList != null)
                        foreach (var daydetailitem in daydetail.DetailList)
                            if (todayAttendanceList.Where(e => e.ClockInType == int.Parse(daydetailitem.ClockInType) && e.ClockInTime == DateTime.Parse(daydetailitem.ClockInTime)).Count() == 0)
                            {
                                insertIdent = true;
                                pushMessage = "数据已同步\n" + (int.Parse(daydetailitem.ClockInType) == 0 ? "签到时间:" : "签退时间:") + daydetailitem.ClockInTime;
                                if (int.Parse(daydetailitem.ClockInType) == 1) signout = true;
                            }

            if (insertIdent)
            {
                dbConnection.Execute($"delete from public.attendancerecord where attendancemonth = '{startDate:yyyy-MM}'");
                dbConnection.Execute($"delete from public.attendancerecordday where to_char(attendancedate,'yyyy-mm') = '{startDate:yyyy-MM}'");
                dbConnection.Execute($"delete from public.attendancerecorddaydetail where to_char(attendancedate,'yyyy-mm') = '{startDate:yyyy-MM}'");
                dbConnection.Execute(
                    $"INSERT INTO public.attendancerecord(attendancemonth,workdays,latedays,earlydays) VALUES('{startDate:yyyy-MM}',{resultModel.Data.WorkDays},{resultModel.Data.LateDays},{resultModel.Data.EarlyDays});");
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

                pushMessageHelper.Push("考勤", pushMessage, PushMessageHelper.PushIcon.Attendance);
                if (signout)
                {
                    workFlowExecutor.ExecuteAll();
                    pmisHelper.RealOverTimeList();
                }
            }
        }

        dbConnection.Dispose();
    }

    /// <summary>
    /// Keep数据同步
    /// </summary>
    public void KeepRecord()
    {
        #region V2

        if (string.IsNullOrEmpty(configuration["KEEPx-bundleId"])
            || string.IsNullOrEmpty(configuration["KEEPx-session-id"])
            || string.IsNullOrEmpty(configuration["KEEPCookie"])
            || string.IsNullOrEmpty(configuration["KEEPx-user-id"])
            || string.IsNullOrEmpty(configuration["KEEPx-keep-timezone"])
            || string.IsNullOrEmpty(configuration["KEEPAuthorization"]))
            return;

        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-bundleId", configuration["KEEPx-bundleId"]);
        client.DefaultRequestHeaders.Add("x-session-id", configuration["KEEPx-session-id"]);
        client.DefaultRequestHeaders.Add("Cookie", configuration["KEEPCookie"]);
        client.DefaultRequestHeaders.Add("x-user-id", configuration["KEEPx-user-id"]);
        client.DefaultRequestHeaders.Add("x-keep-timezone", configuration["KEEPx-keep-timezone"]);
        client.DefaultRequestHeaders.Add("Authorization", configuration["KEEPAuthorization"]);
        var response = client.GetAsync("https://api.gotokeep.com/pd/v3/stats/detail?dateUnit=all").Result;
        var result = response.Content.ReadAsStringAsync().Result;
        var resultModel = JsonConvert.DeserializeObject<KeepResponse>(result);
        if (resultModel is { ok: true })
            foreach (var logitem in resultModel.data.records.SelectMany(item => item.logs))
            {
                if (logitem.stats == null) continue;
                dbConnection.Execute($@"delete from public.eventinfo where source = :source and distinguishingmark=:distinguishingmark",
                    new { source = "keep", distinguishingmark = logitem.stats.id });
                if (logitem.stats.type != "training")
                {
                    // 转换为TimeSpan 
                    var span = TimeSpan.FromMilliseconds(Math.Abs(logitem.stats.endTime - logitem.stats.startTime));
                    dbConnection.Execute(
                        $@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                        , new
                        {
                            id = Guid.NewGuid().ToString(),
                            title = logitem.stats.name + logitem.stats.nameSuffix,
                            message = "用时 " + $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2};消耗 " + logitem.stats.calorie + "千卡",
                            clockintime = DateTime.Parse(logitem.stats.doneDate).ToString("yyyy-MM-dd HH:mm:ss"),
                            color = "green",
                            source = "keep",
                            distinguishingmark = logitem.stats.id
                        });
                }
                else
                {
                    var traresponse = client.GetAsync("https://api.gotokeep.com/minnow-webapp/v1/sportlog/" + logitem.stats.id).Result;
                    var traresult = traresponse.Content.ReadAsStringAsync().Result;
                    var traResultModel = JsonConvert.DeserializeObject<SportLogResponse>(traresult);
                    if (traResultModel is not { ok: true }) continue;
                    var sportLogSectionsModel = traResultModel.data.sections.FirstOrDefault(e => e.style.ToLower() == "sportdata");
                    if (sportLogSectionsModel != null)
                    {
                        var sportLogContentListTime = sportLogSectionsModel.content.list.FirstOrDefault(e => e.title == "训练时长");
                        var sportLogContentListDistance = sportLogSectionsModel.content.list.FirstOrDefault(e => e.title == "总距离");
                        if (sportLogContentListTime != null && sportLogContentListDistance != null)
                            dbConnection.Execute(
                                $@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                                , new
                                {
                                    id = Guid.NewGuid().ToString(),
                                    title = logitem.stats.name + logitem.stats.nameSuffix + sportLogContentListDistance.valueStr + sportLogContentListDistance.unit,
                                    message = "用时 " + sportLogContentListTime.valueStr + ";消耗 " + logitem.stats.calorie + "千卡",
                                    clockintime = DateTime.Parse(logitem.stats.doneDate).ToString("yyyy-MM-dd HH:mm:ss"),
                                    color = "green",
                                    source = "keep",
                                    distinguishingmark = logitem.stats.id
                                });
                    }
                }
            }

        dbConnection.Dispose();

        #endregion

        #region V1

        //IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
        //HttpClient client = new HttpClient();
        //client.DefaultRequestHeaders.Add("x-bundleId", _Configuration["KEEPx-bundleId"]);
        //client.DefaultRequestHeaders.Add("x-session-id", _Configuration["KEEPx-session-id"]);
        //client.DefaultRequestHeaders.Add("Cookie", _Configuration["KEEPCookie"]);
        //client.DefaultRequestHeaders.Add("x-user-id", _Configuration["KEEPx-user-id"]);
        //client.DefaultRequestHeaders.Add("x-keep-timezone", _Configuration["KEEPx-keep-timezone"]);
        //client.DefaultRequestHeaders.Add("Authorization", _Configuration["KEEPAuthorization"]);
        //DateTime dtNow = DateTime.Now;
        //var response = client.GetAsync("https://api.gotokeep.com/feynman/v8/data-center/sub/sport-log/card/SPORT_LOG_LIST_CARD?sportType=all&dateUnit=daily&date=" + dtNow.ToString("yyyyMMdd")).Result;
        //string result = response.Content.ReadAsStringAsync().Result;
        //KeepResponse ResultModel = JsonConvert.DeserializeObject<KeepResponse>(result);
        //if (ResultModel.ok)
        //{
        //    foreach (var item in ResultModel.data.data.dailyList)
        //    {
        //        foreach (var Logitem in item.logList)
        //        {
        //            _DbConnection.Execute($@"delete from public.eventinfo where source = :source and distinguishingmark=:distinguishingmark", new { source = "keep", distinguishingmark = Logitem.id });
        //            _DbConnection.Execute($@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
        //                           , new
        //                           {
        //                               id = Guid.NewGuid().ToString(),
        //                               title = Logitem.name + Logitem.nameSuffix,
        //                               message = string.Join(';', Logitem.indicatorList),
        //                               clockintime = dtNow.ToString("yyyy-MM-dd") + " " + Logitem.endTimeText + ":00",
        //                               color = "green",
        //                               source = "keep",
        //                               distinguishingmark = Logitem.id
        //                           });
        //        }
        //    }
        //}
        //_DbConnection.Dispose();

        #endregion
    }

    /// <summary>
    /// 高危人员打卡预警
    /// 每小时的5分，35分执行一次，查询当前是否为休息日，如果是休息日则进行token伪造，同时根据配置中的人员名单查询名单内人员的考勤记录；如存在高危人员打卡，则进行消息提醒，同时将提醒过的内容写入缓存，缓存有效期20小时，避免重复提醒
    /// </summary>
    public void CheckInWarning()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
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
        const string fakeSignature = "Gj0IbFZe_rpj5mtMfwoHVo2luGHlmaJa7MtbxwfNSaI";
        var listOfPersonnel = configuration.GetSection("ListOfPersonnel").Get<List<ListOfPersonnel>>();
        if (listOfPersonnel != null)
        {
            var realNameList = listOfPersonnel.Select(e => e.RealName).ToList();
            using var sr = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "AddressBook.json", Encoding.UTF8);
            var content = sr.ReadToEnd();
            var addressBookList = JsonConvert.DeserializeObject<List<AddressBookInfo>>(content);

            addressBookList = addressBookList?.Where(e => realNameList.Contains(e.Name)).ToList();
            if (addressBookList == null) return;
            var header = new
            {
                typ = "JWT",
                alg = "HS256"
            };
            var headerJson = JsonConvert.SerializeObject(header);
            var headerBase64 = Base64UrlEncode(headerJson);
            var startDate = DateTime.Now;
            foreach (var addressBookItem in addressBookList)
            {
                var waringcount = dbConnection.Query<int>(
                    $@"SELECT COUNT(0) FROM public.checkinwarning WHERE name = '{addressBookItem.Name}' AND TO_CHAR(clockintime,'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}'").First();
                if (waringcount > 0) continue;
                var payload = new
                {
                    iat = 1734922017,
                    id = addressBookItem.Id,
                    jwtId = "2318736ce27645c39729dd6cbf6e3232",
                    uid = addressBookItem.Id,
                    tenantId = "5d89917712441d7a5073058c",
                    cid = "5d89917712441d7a5073058c",
                    mainId = addressBookItem.MainId,
                    avatar = addressBookItem.Avatar,
                    name = addressBookItem.Name,
                    account = addressBookItem.Sn,
                    mobile = addressBookItem.Mobile,
                    sn = addressBookItem.Sn,
                    group = "6274c1d256a7b338c43fb328",
                    groupName = "04.管网管理产线",
                    yhloNum = addressBookItem.YhloNum,
                    isAdmin = false,
                    channel = "app",
                    roles = new[]
                    {
                        "6479adb956a7b33dbcce610c",
                        "1826788029153456129",
                        "6332ce1b56a7b316e0574808",
                        "1775433892067655682",
                        "1749600164359757825"
                    },
                    company = new
                    {
                        id = "6274c1d256a7b338c43fb328",
                        name = "04.管网管理产线",
                        code = "647047646"
                    },
                    tokenfrom = "uniwim",
                    userType = "user",
                    exp = 1735008477
                };
                var payloadJson = JsonConvert.SerializeObject(payload);
                var payloadBase64 = Base64UrlEncode(payloadJson);
                var jwt = $"{headerBase64}.{payloadBase64}.{fakeSignature}";
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", jwt);
                var response = client.GetAsync(pmisInfo.Url + "/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + startDate.ToString("yyyy-MM")).Result;
                var result = response.Content.ReadAsStringAsync().Result;
                var resultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
                if (resultModel is not { Code: 200 }) continue;
                var dayAttendanceList = resultModel.Data.DayVoList.FirstOrDefault(e => e.Day == DateTime.Now.Day);
                if (dayAttendanceList == null) continue;
                {
                    if (dayAttendanceList.DetailList == null) continue;
                    foreach (var day in dayAttendanceList.DetailList)
                        switch (day.ClockInType)
                        {
                            case "0" when day.ClockInStatus == 1 && day.ClockInStatus != 999:
                                pushMessage += listOfPersonnel.FirstOrDefault(e => e.RealName == addressBookItem.Name)?.FlowerName + "-上班时间:" +
                                               DateTime.Parse(day.ClockInTime).ToString("HH:mm:ss") + "\n";
                                dbConnection.Execute($@"INSERT INTO public.checkinwarning(id,name,clockintime) VALUES('{Guid.NewGuid()}','{addressBookItem.Name}','{day.ClockInTime}')");
                                break;
                        }
                }
            }
        }

        if (string.IsNullOrEmpty(pushMessage)) return;
        pushMessageHelper.Push("高危人员打卡提醒", pushMessage, PushMessageHelper.PushIcon.Camera);
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// 同步禅道任务
    /// 每30分钟一次，获取禅道token,并从禅道任务列表读取我的任务，通过获取到的我的任务列表中的projectid字段，通过禅道项目名循环查询接口获取每条任务的所属项目编码，并将处理完成的任务插入进本地禅道任务表，该任务具有幂等性。
    /// </summary>
    public void SynchronizationZentaoTask()
    {
        zentaoHelper.SynchronizationZentaoTask();
    }

    /// <summary>
    /// 执行禅道完成任务、日报、周报发送
    /// 每40分钟一次，获取到当日工时大于0以后，查询当日是否已发送日报，如未发送，则执行禅道完成任务接口，登记工时时，会根据当日总工时，分摊至当日所有禅道工单内，每条工单被分配的工时不会超过预估工时上限，禅道处理完成后会进行推送通知，
    /// 同时继续执行日报发送工作，判断当日禅道工单数量大于0且工单状态都已处于"done"完成状态，则发送日报；日报发送成功后会进行推送通知；
    /// 判断当日的后一天是否为休息日，如果是休息日，则判断本周是否已发送周报，如果周报未发送，则发送周报，周报总结会使用DeepSeek结合本周所有工作内容进行生成，发送成功后会进行推送通知。
    /// 该任务具有幂等性
    /// </summary>
    public void ExecuteAllWork()
    {
        workFlowExecutor.ExecuteAll();
    }

    /// <summary>
    /// 自动提交加班申请
    /// 每30分钟一次，每日13：30至20：30内执行，如果当时不为休息日，且加班记录表内没有当时加班信息，则通过查询禅道任务表，并按照剩余工时倒序排列第一次条，按照该条任务内容，通过deepseek生成加班事由提交加班申请；申请成功后会进行推送通知
    /// </summary>
    public void CommitOvertimeWork()
    {
        try
        {
            var projectInfo = new ProjectInfo();
            var workStart = new TimeSpan(13, 30, 0); // 08:30
            var workEnd = new TimeSpan(20, 30, 0); // 20:30
            if (DateTime.Now.TimeOfDay < workStart || DateTime.Now.TimeOfDay > workEnd) return;
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            //判断休息日不提交加班
            var checkinrule = dbConnection.Query<string>($@"select checkinrule from public.attendancerecordday where to_char(attendancedate,'yyyy-MM-dd')  = to_char(now(),'yyyy-MM-dd')")
                .FirstOrDefault();
            if (checkinrule == "休息") return;
            //查询是否打卡上班
            var clockinCount = dbConnection
                .Query<int>($@"SELECT COUNT(0) FROM public.attendancerecorddaydetail WHERE clockintype= '0' AND TO_CHAR(clockintime,'yyyy-MM-dd') = to_char(now(),'yyyy-MM-dd')")
                .FirstOrDefault();
            if (clockinCount == 0) return;
            //查询是否已提交加班申请
            var hasOvertime = dbConnection.Query<int>($@"select count(0) from  public.overtimerecord where work_date = '{DateTime.Now:yyyy-MM-dd}'").FirstOrDefault();
            if (hasOvertime != 0) return;
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

            if (zentaoInfo?.project == null || zentaoInfo?.id == null || string.IsNullOrEmpty(zentaoInfo?.projectcode)) return;
            if (string.IsNullOrEmpty(zentaoInfo?.projectcode)) return;
            if (zentaoInfo?.projectcode == "GIS-Product")
                projectInfo = new ProjectInfo
                {
                    contract_id = "",
                    contract_unit = "",
                    project_name = "GIS外业管理系统"
                };
            else
                projectInfo = pmisHelper.GetProjectInfo(zentaoInfo?.projectcode);

            if (string.IsNullOrEmpty(projectInfo.project_name)) return;
            var chatOptions = new ChatOptions { Tools = [] };
            var chatHistory = new List<ChatMessage>
            {
                new(ChatRole.System, pmisInfo.DailyPrompt),
                new(ChatRole.User, "加班内容：" + zentaoInfo.taskname + ":" + zentaoInfo.taskdesc)
            };
            var res = chatClient.GetResponseAsync(chatHistory, chatOptions).Result;
            if (string.IsNullOrWhiteSpace(res?.Text)) return;
            var workContent = res.Text;
            if (string.IsNullOrEmpty(workContent)) return;
            var insertId = pmisHelper.OvertimeWork_Insert(projectInfo, zentaoInfo?.id.ToString(), workContent);
            if (string.IsNullOrEmpty(insertId)) return;
            var processId = pmisHelper.OvertimeWork_CreateOrder(projectInfo, insertId, zentaoInfo?.id.ToString(), workContent);
            if (!string.IsNullOrEmpty(processId))
            {
                JObject updateResult = pmisHelper.OvertimeWork_Update(projectInfo, insertId, zentaoInfo?.id.ToString(), processId, workContent);
                if (updateResult["Response"] != null)
                {
                    pushMessageHelper.Push("加班申请", DateTime.Now.ToString("yyyy-MM-dd") + " 加班申请已提交\n加班事由：" + workContent, PushMessageHelper.PushIcon.OverTime);
                    dbConnection.Execute($@"
                                      insert
                                      	into
                                      	public.overtimerecord
                                      (id,
                                      	plan_start_time,
                                      	plan_end_time,
                                      	plan_work_overtime_hour,
                                      	contract_id,
                                      	contract_unit,
                                      	project_name,
                                      	work_date,
                                      	subject_matter,
                                      	orderid)
                                      values('{Guid.NewGuid().ToString()}',
                                      '{updateResult["Response"]?["plan_start_time"]}',
                                      '{updateResult["Response"]?["plan_end_time"]}',
                                      {updateResult["Response"]?["plan_work_overtime_hour"]},
                                      '{updateResult["Response"]?["contract_id"]}',
                                      '{updateResult["Response"]?["contract_unit"]}',
                                      '{updateResult["Response"]?["project_name"]}',
                                      '{updateResult["Response"]?["work_date"]}',
                                      '{updateResult["Response"]?["subject_matter"]}',
                                      '{updateResult["Response"]?["id"]}');");
                }
            }
        }
        catch (Exception e)
        {
            pushMessageHelper.Push("加班申请异常", e.Message, PushMessageHelper.PushIcon.Alert);
        }
    }

    /// <summary>
    /// 禅道衡量目标、计划完成成果、实际从事工作与成果信息补全
    /// 每30分钟一次，查询已同步的禅道工单，如存在衡量目标、计划完成成果、实际从事工作与成果字段为空的数据，则根据任务内容，通过deepseek生成补全信息，进行补全。补全后会在提交日报时使用，补全完成后会进行推送通知
    /// </summary>
    public void TaskDescriptionComplete()
    {
        zentaoHelper.TaskDescriptionComplete();
    }

    /// <summary>
    /// DeepSeek余额预警
    /// 每2小时一次，调用deepseek余额查询接口，如可用余额低于1元则进行推送通知
    /// </summary>
    public void DeepSeekBalance()
    {
        var httpRequestHelper = new HttpRequestHelper();
        var postResponse = httpRequestHelper.GetAsync(configuration["LLM:EndPoint"] + "/user/balance",
            new Dictionary<string, string> { { "Authorization", "Bearer " + configuration["LLM:ApiKey"] } }).Result;
        if (postResponse.IsSuccessStatusCode)
        {
            decimal total_balance = 0;
            var pushMessage = "";
            var json = JObject.Parse(postResponse.Content.ReadAsStringAsync().Result);
            if (bool.Parse(json["is_available"].ToString()))
                pushMessage += "尚有余额可供使用";
            else
                pushMessage += "已无余额可供使用";

            if (!string.IsNullOrEmpty(json["balance_infos"].ToString()))
                if (json["balance_infos"] is JArray dataArray)
                    foreach (var jToken in dataArray)
                    {
                        pushMessage += $"\n可用余额: " + jToken["total_balance"] + " " + jToken["currency"];
                        total_balance += decimal.Parse(jToken["total_balance"].ToString());
                    }

            if (!bool.Parse(json["is_available"].ToString()) || total_balance <= 1) pushMessageHelper.Push("余额提醒", pushMessage, PushMessageHelper.PushIcon.DeepSeek);
        }
    }

    /// <summary>
    /// 实际加班任务处理
    /// 每天9点一次，提交所有待处理实际加班，关闭超2天无效加班
    /// </summary>
    public void RealOverTime()
    {
        pmisHelper.RealOverTimeList();
    }
}
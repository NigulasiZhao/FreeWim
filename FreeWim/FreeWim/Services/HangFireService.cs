using Hangfire;
using Hangfire.Storage;

namespace FreeWim.Services;

public class HangFireService(
    IConfiguration configuration,
    AttendanceService attendanceService,
    KeepDataSyncService keepDataSyncService,
    DeepSeekMonitorService deepSeekMonitorService,
    MessageService messageService,
    ZentaoService zentaoService,
    PmisService pmisService,
    WorkFlowExecutorService workFlowExecutorService,
    SpeedTestService speedTestService,
    AsusRouterService asusRouterService,
    YhloWebSocketService yhloWebSocketService)
{
    public void StartHangFireTask()
    {
        //每日零点0 0 0 */1 * ?
        //每小时0 0 * * * ?
        //每五分钟0 0/5 * * * ?
        //RecurringJob.AddOrUpdate("SpeedTest", () => SpeedTest(), "0 0 */1 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

        using (var connection = JobStorage.Current.GetConnection())
        {
            var recurringJobs = connection.GetRecurringJobs();

            foreach (var job in recurringJobs) RecurringJob.RemoveIfExists(job.Id);
        }

        RecurringJob.AddOrUpdate("考勤同步", () => attendanceService.SyncAttendanceRecord(), "5,35 * * * *", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("Keep数据同步", () => keepDataSyncService.SyncKeepData(), "0 0 */3 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("高危人员打卡预警", () => attendanceService.CheckInWarning(), "0 0/5 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("同步禅道任务", () => zentaoService.SynchronizationZentaoTask(), "0 15,17,19 * * *", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("执行禅道完成任务、日报、周报发送", () => workFlowExecutorService.ExecuteAll(), "0 0/40 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("自动加班申请", () => pmisService.CommitOvertimeWork(), "0 0/30 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("禅道衡量目标、计划完成成果、实际从事工作与成果信息补全", () => zentaoService.TaskDescriptionComplete(), "0 0/30 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("DeepSeek余额预警", () => deepSeekMonitorService.CheckBalance(), "0 0 */2 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("提交所有待处理实际加班申请", () => pmisService.RealOverTimeList(), "0 0 9 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("餐补提醒", () => pmisService.MealAllowanceReminder(), "0 0 14 24,25,26 * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("一诺自动聊天", () => messageService.AutomaticallySendMessage(), "0 0/10 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("网络测速", () => speedTestService.ExecuteSpeedTestAsync(), "0 0 1 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("网络异常提醒", () => speedTestService.CheckSpeedAbnormal(), "0 0 10 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

        if (!string.IsNullOrEmpty(configuration.GetValue<string>("AsusRouter:RouterIp")))
        {
            RecurringJob.AddOrUpdate("路由器设备同步", () => asusRouterService.SyncRouterDevices(), "0 0 1 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
            RecurringJob.AddOrUpdate("设备流量统计", () => asusRouterService.SyncDeviceTraffic(), "0 0 2 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        }

        // WebSocket连接任务 - 每小时初始化一次连接
        RecurringJob.AddOrUpdate("YHLO WebSocket连接", () => yhloWebSocketService.InitializeConnection(), "0 0 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        // 心跳任务 - 每30秒发送一次
        RecurringJob.AddOrUpdate("YHLO WebSocket心跳", () => yhloWebSocketService.SendHeartbeat(), "*/30 * * * * *", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
    }
}

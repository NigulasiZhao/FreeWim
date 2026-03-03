namespace FreeWim.Models.Attendance.Dto;

/// <summary>
/// 高级工时统计输出模型
/// </summary>
public class AdvancedWorkHoursStatisticsOutput
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 用户姓名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 部门名称
    /// </summary>
    public string? OrgName { get; set; }

    /// <summary>
    /// 用户编号
    /// </summary>
    public string? UserSn { get; set; }

    /// <summary>
    /// 基础应出勤天数
    /// </summary>
    public double BaseWorkingDays { get; set; }

    /// <summary>
    /// 出差天数
    /// </summary>
    public double OutDays { get; set; }

    /// <summary>
    /// 调休假天数
    /// </summary>
    public double OffsetLeaveDays { get; set; }

    /// <summary>
    /// 一般请假天数
    /// </summary>
    public double GeneralLeaveDays { get; set; }

    /// <summary>
    /// 实际出勤天数
    /// </summary>
    public double ActualAttendanceDays { get; set; }

    /// <summary>
    /// 非周末加班小时数
    /// </summary>
    public double DelayOvertimeHours { get; set; }

    /// <summary>
    /// 周末加班小时数
    /// </summary>
    public double WeekendOvertimeHours { get; set; }

    /// <summary>
    /// 总加班小时数
    /// </summary>
    public double TotalOvertimeHours { get; set; }

    /// <summary>
    /// 实际工作工时
    /// </summary>
    public double TotalActualWorkHours { get; set; }

    /// <summary>
    /// 日均实际工时
    /// </summary>
    public double DailyAvgWorkHours { get; set; }
}
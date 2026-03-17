using System.ComponentModel;

namespace FreeWim.Models.Attendance.Dto;

public class UserClockInDetailsInput
{
    [Description("用户ID")] public string? UserId { get; set; }
    [Description("用户名")] public string? UserName { get; set; }
    [Description("开始时间")] public string? StartTime { get; set; }
    [Description("结束时间")] public string? EndTime { get; set; }
}

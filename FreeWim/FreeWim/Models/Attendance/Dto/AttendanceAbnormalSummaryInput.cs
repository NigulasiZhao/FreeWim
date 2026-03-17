using System.ComponentModel;
namespace FreeWim.Models.Attendance.Dto;

public class AttendanceAbnormalSummaryInput
{
    [Description("开始时间")] public string? StartTime { get; set; }
    [Description("结束时间")] public string? EndTime { get; set; }
    [Description("组织名称")] public string? OrgName { get; set; }
    [Description("用户姓名")] public string? UserName { get; set; }
    [Description("页码")] public int Page { get; set; } = 1;
    [Description("每页行数")] public int Rows { get; set; } = 100;
    [Description("排序字段")] public string? Order { get; set; } = "TotalAbnormalDays";
    [Description("排序方式")] public string? Sort { get; set; } = "desc";
}

namespace FreeWim.Models.Attendance.Dto;

public class AttendanceAbnormalSummaryInput
{
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? OrgName { get; set; }
    public string? UserName { get; set; }
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 100;
    public string? Order { get; set; } = "TotalAbnormalDays";
    public string? Sort { get; set; } = "desc";
}

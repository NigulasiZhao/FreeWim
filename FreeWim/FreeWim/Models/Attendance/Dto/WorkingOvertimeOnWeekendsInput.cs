namespace FreeWim.Models.Attendance.Dto;

public class WorkingOvertimeOnWeekendsInput
{
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 10;
    public string? Order { get; set; } = "statisticsdate";
    public string? Sort { get; set; } = "desc";
}
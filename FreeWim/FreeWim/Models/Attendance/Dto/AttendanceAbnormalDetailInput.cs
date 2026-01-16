namespace FreeWim.Models.Attendance.Dto;

public class AttendanceAbnormalDetailInput
{
    public string? UserId { get; set; }
    public string? OrgId { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? UserName { get; set; }
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 100;
    public string? Order { get; set; } = "ClockInDate";
    public string? Sort { get; set; } = "asc";
}
public class RangeActionInput
{
    public int Type { get; set; }
}
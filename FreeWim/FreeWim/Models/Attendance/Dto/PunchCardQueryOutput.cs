namespace FreeWim.Models.Attendance.Dto;

public class PunchCardQueryOutput
{
    public string? Ename { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
namespace FreeWim.Models.Attendance.Dto;

public class UserClockInDetailsInput
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

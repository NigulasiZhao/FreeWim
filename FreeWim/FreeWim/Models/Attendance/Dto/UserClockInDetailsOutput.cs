namespace FreeWim.Models.Attendance.Dto;

public class UserClockInDetailsOutput
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? OrgName { get; set; }
    public string? ClockInDate { get; set; }
    public string? DayOfWeek { get; set; }
    public string? CheckInRule { get; set; }
    public double? WorkHours { get; set; }
    public int? WorkMinutes { get; set; }
    public int? ClockInNumber { get; set; }
    public string? IsLate { get; set; }
    public string? IsEarly { get; set; }
    public string? IsAbsenteeism { get; set; }
    public string? IsRest { get; set; }
    public string? IsOut { get; set; }
    public int? WorkOvertime { get; set; }
    public double? LeaveHours { get; set; }
    public string? LeaveType { get; set; }
}

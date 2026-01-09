namespace FreeWim.Models.Attendance.Dto;

public class AttendanceAbnormalDetailOutput
{
    public string? ClockInDate { get; set; }
    public string? UserName { get; set; }
    public string? ActualClockInTime { get; set; }
    public string? AbnormalStatus { get; set; }
    public int TotalAbnormalMinutes { get; set; }
    public string? RemarkSummary { get; set; }
}

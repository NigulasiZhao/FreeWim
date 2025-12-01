namespace FreeWim.Models.Attendance.Dto;

public class WorkingOvertimeOnWeekendsIOutput
{
    public string? Name { get; set; }
    public string? StatisticsDate { get; set; }
    public DateTime SignInTime { get; set; }
    public DateTime SignOutTime { get; set; }
    public double HoursDiff { get; set; }
}
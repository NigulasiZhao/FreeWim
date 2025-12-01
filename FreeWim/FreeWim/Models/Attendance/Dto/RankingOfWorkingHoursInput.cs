namespace FreeWim.Models.Attendance.Dto;

public class RankingOfWorkingHoursInput
{
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? OrgName { get; set; }
    public string? UserName { get; set; }
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 10;
    public string? Order { get; set; } = "total_work_hours";
    public string? Sort { get; set; } = "desc";
}
namespace FreeWim.Models.Attendance.Dto;

public class RankingOfWorkingHoursOutput
{
    public string? UserName { get; set; }
    public string? UserId { get; set; }
    public string? OrgName { get; set; }
    public double TotalWorkHours { get; set; }
    public double TotalOvertime { get; set; }
    public double WorkRank { get; set; }
    public double WorkSurpassedCount { get; set; }
    public double WorkSurpassedPercent { get; set; }
    public double OvertimeRank { get; set; }
    public double OvertimeSurpassedCount { get; set; }
    public double OvertimeSurpassedPercent { get; set; }
    public string? RankDescription { get; set; }
}
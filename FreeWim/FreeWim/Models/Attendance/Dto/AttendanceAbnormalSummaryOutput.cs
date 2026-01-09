namespace FreeWim.Models.Attendance.Dto;

public class AttendanceAbnormalSummaryOutput
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? OrgId { get; set; }
    public string? OrgName { get; set; }
    public int MissingCardDays { get; set; }
    public int EarlyLeaveDays { get; set; }
    public int LateAndEarlyLeaveDays { get; set; }
    public int FieldLateDays { get; set; }
    public int FieldEarlyLeaveDays { get; set; }
    public int SupplementCardDays { get; set; }
    public int CompensatoryLeaveDays { get; set; }
    public int TotalLeaveDays { get; set; }
    public int TotalAbnormalDays { get; set; }
}

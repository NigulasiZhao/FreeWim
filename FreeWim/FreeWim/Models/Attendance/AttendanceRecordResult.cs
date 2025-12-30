namespace FreeWim.Models.Attendance;

public class AttendanceRecordResult
{
    public int WorkDays { get; set; }
    public int LateDays { get; set; }
    public int EarlyDays { get; set; }
    public double DayAvg { get; set; }
}

public class AttendanceResponse
{
    public int Code { get; set; }
    public bool Success { get; set; }
    public Data? Data { get; set; }
    public string? Msg { get; set; }
}

public class Data
{
    public int WorkDays { get; set; }
    public int LateDays { get; set; }
    public int EarlyDays { get; set; }
    public List<DayVo>? DayVoList { get; set; }
}

public class DayVo
{
    public bool UntilThisDay { get; set; }
    public int? Day { get; set; }
    public string? CheckInRule { get; set; }
    public string? IsNormal { get; set; }
    public string? IsAbnormal { get; set; }
    public string? IsApply { get; set; }
    public int ClockInNumber { get; set; }
    public List<Detail>? DetailList { get; set; }
    public double? WorkHours { get; set; }
}

public class Detail
{
    public int Id { get; set; }
    public int? RecordId { get; set; }
    public string? ClockInType { get; set; }
    public string? ClockInTime { get; set; }
    public string? ClockInPosition { get; set; }
    public string? ClockInPositionCoordinate { get; set; }
    public int? ClockInStatus { get; set; }
    public string? ClockInStatusName { get; set; }
    public string? Remark { get; set; }
    public string? Picture { get; set; }
    public int? OrderNo { get; set; }
    public string? ClockInTimeChange { get; set; }
    public string? ClockInStatusChange { get; set; }
    public string? ClockInStatusNameChange { get; set; }
    public int? RealClockInStatus { get; set; }
    public string? RealClockInTime { get; set; }
    public string? LaterEarlyMinutes { get; set; }
    public int? ClockMethod { get; set; }
}

public class WeekSummaryDto
{
    public string LastDay { get; set; } = string.Empty;
    public double WeekHours { get; set; }
}

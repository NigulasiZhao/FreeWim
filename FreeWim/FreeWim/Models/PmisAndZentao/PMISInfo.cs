namespace FreeWim.Models.PmisAndZentao;

public class PMISInfo
{
    public required string Url { get; set; }
    public required string UserId { get; set; }
    public required string UserAccount { get; set; }
    public required string PassWord { get; set; }

    public required string UserName { get; set; }
    public required string UserMobile { get; set; }

    public required string OverStartTime { get; set; }

    public required string OverEndTime { get; set; }
    public required string WorkType { get; set; }
    public required string WorkContent { get; set; }
    public required string DlmeasureUrl { get; set; }
    public required string DailyWorkPrompt { get; set; }
    public required string DailyPrompt { get; set; }
    public required string WeekPrompt { get; set; }

    public required string? AppId { get; set; }
    public required string? App { get; set; }
    public required string ZkUrl { get; set; }
    public required string ZkSN { get; set; }
}

public class PMISInsertResponse
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public bool Success { get; set; }
}

public class WeekDayInfo
{
    public int WeekNumber { get; set; }
    public string? StartOfWeek { get; set; }
    public string? EndOfWeek { get; set; }
}
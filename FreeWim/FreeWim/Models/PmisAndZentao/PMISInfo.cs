namespace FreeWim.Models.PmisAndZentao;

public class PMISInfo
{
    public string Url { get; set; }
    public string UserId { get; set; }
    public string UserAccount { get; set; }
    public string PassWord { get; set; }

    public string UserName { get; set; }
    public string UserMobile { get; set; }

    public string OverStartTime { get; set; }

    public string OverEndTime { get; set; }
    public string WorkType { get; set; }
    public string WorkContent { get; set; }
    public string DlmeasureUrl { get; set; }
    public string DailyWorkPrompt { get; set; }
    public string DailyPrompt { get; set; }
    public string WeekPrompt { get; set; }

    public string AppId { get; set; }
    public string App { get; set; }
}

public class PMISInsertResponse
{
    public int Code { get; set; }
    public string Message { get; set; }
    public bool Success { get; set; }
}

public class WeekDayInfo
{
    public int WeekNumber { get; set; }
    public string StartOfWeek { get; set; }
    public string EndOfWeek { get; set; }
}
namespace FreeWim.Models.Gogs;

public class GogsCalendar
{
    public string? rownum { get; set; }
    public string? title { get; set; }
    public string? airDateUtc { get; set; }
    public bool hasFile { get; set; }
    public string? message { get; set; }

    public string? color { get; set; }
}
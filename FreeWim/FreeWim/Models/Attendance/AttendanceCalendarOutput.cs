namespace FreeWim.Models.Attendance;

public class AttendanceCalendarOutput
{
    public int rownum { get; set; }
    public string title { get; set; }
    public string airDateUtc { get; set; }
    public bool hasFile { get; set; }
    public string workhours { get; set; }
}
namespace FreeWim.Models.Attendance;

public class AutoCheckInRecord
{
    public string id { get; set; }
    public string jobid { get; set; }

    public DateTime clockintime { get; set; }

    public int clockinstate { get; set; }
}
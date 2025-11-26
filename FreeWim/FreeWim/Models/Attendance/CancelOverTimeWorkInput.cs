namespace FreeWim.Models.Attendance;

public class CancelOverTimeWorkInput
{
    public string? date { get; set; }
    public string? contractunit { get; set; }
    public string? start { get; set; }
    public string? end { get; set; }
    public string? duration { get; set; }
    public string? status { get; set; }
}
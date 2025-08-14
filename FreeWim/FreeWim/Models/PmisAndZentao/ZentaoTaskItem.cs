namespace FreeWim.Models.PmisAndZentao;

public class TaskItem
{
    public int Id { get; set; }
    public double Timeleft { get; set; }
    public double TimeConsuming { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class FinishZentaoTaskResponse
{
    public int id { get; set; }
    public float consumed { get; set; }
    public string status { get; set; }
}
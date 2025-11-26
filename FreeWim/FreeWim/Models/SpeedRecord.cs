namespace FreeWim.Models;

public class SpeedRecord
{
    public string? id { get; set; }
    public string? ping { get; set; }
    public double? download { get; set; }
    public double? upload { get; set; }
    public int? server_id { get; set; }
    public string? server_host { get; set; }
    public string? server_name { get; set; }
    public string? url { get; set; }
    public int? scheduled { get; set; }
    public int? failed { get; set; }
    public DateTime? created_at { get; set; }
    public DateTime? updated_at { get; set; }
}

public class SpeedRecordResponse
{
    public string? Message { get; set; }
    public SpeedRecord? Data { get; set; }
}
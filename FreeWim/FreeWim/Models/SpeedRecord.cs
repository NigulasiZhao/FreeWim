namespace FreeWim.Models;

/// <summary>
/// 数据库实体类
/// </summary>
public class SpeedRecord
{
    public string? id { get; set; }
    public string? ping { get; set; }
    public decimal? download { get; set; }
    public decimal? upload { get; set; }
    public double? server_id { get; set; }
    public string? server_host { get; set; }
    public string? server_name { get; set; }
    public string? url { get; set; }
    public double? scheduled { get; set; }
    public double? failed { get; set; }
    public DateTime? created_at { get; set; }
    public DateTime? updated_at { get; set; }
}

/// <summary>
/// API 返回的 DTO（友好格式）
/// </summary>
public class SpeedRecordDto
{
    public string? Id { get; set; }
    public string? Ping { get; set; }
    public string? Download { get; set; }  // Mbit/s
    public string? Upload { get; set; }    // Mbit/s
    public string? ServerName { get; set; }
    public string? ServerHost { get; set; }
    public string? Url { get; set; }
    public DateTime? TestTime { get; set; }
}

public class SpeedRecordResponse
{
    public string? Message { get; set; }
    public SpeedRecordDto? Data { get; set; }
}

/// <summary>
/// 图表数据 DTO（用于前端图表展示）
/// </summary>
public class SpeedRecordForChart
{
    public string? id { get; set; }
    public DateTime? created_at { get; set; }
    public decimal? download { get; set; }
    public decimal? upload { get; set; }
    public decimal? ping { get; set; }
    public string? server_name { get; set; }
}
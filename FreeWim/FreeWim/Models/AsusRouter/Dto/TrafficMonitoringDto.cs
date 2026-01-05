namespace FreeWim.Models.AsusRouter.Dto;

/// <summary>
/// 流量监控页面数据DTO
/// </summary>
public class TrafficMonitoringDto
{
    /// <summary>
    /// KPI统计数据
    /// </summary>
    public KpiStatistics Kpi { get; set; } = new();

    /// <summary>
    /// 设备列表
    /// </summary>
    public List<DeviceTrafficSummary> Devices { get; set; } = new();

    /// <summary>
    /// 每日流量趋势（所有设备汇总）
    /// </summary>
    public List<DailyTrafficTrend> DailyTrends { get; set; } = new();

    /// <summary>
    /// 应用流量分布（选定周期内）
    /// </summary>
    public List<AppTrafficDistribution> AppDistributions { get; set; } = new();
}

/// <summary>
/// KPI统计数据
/// </summary>
public class KpiStatistics
{
    /// <summary>
    /// 累计上行流量（字节）
    /// </summary>
    public long TotalUploadBytes { get; set; }

    /// <summary>
    /// 累计下行流量（字节）
    /// </summary>
    public long TotalDownloadBytes { get; set; }

    /// <summary>
    /// 累计上行流量（格式化，GB/TB）
    /// </summary>
    public string TotalUploadFormatted { get; set; } = string.Empty;

    /// <summary>
    /// 累计下行流量（格式化，GB/TB）
    /// </summary>
    public string TotalDownloadFormatted { get; set; } = string.Empty;

    /// <summary>
    /// 日均上传（格式化）
    /// </summary>
    public string AvgDailyUpload { get; set; } = string.Empty;

    /// <summary>
    /// 日均下载（格式化）
    /// </summary>
    public string AvgDailyDownload { get; set; } = string.Empty;

    /// <summary>
    /// 统计天数
    /// </summary>
    public int DayCount { get; set; }
}

/// <summary>
/// 设备流量汇总
/// </summary>
public class DeviceTrafficSummary
{
    /// <summary>
    /// 设备ID（MAC地址或"all"表示所有设备）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 设备名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 设备图标（emoji或图标代码）
    /// </summary>
    public string Icon { get; set; } = "📱";

    /// <summary>
    /// 上行流量（格式化，如 "22.4GB"）
    /// </summary>
    public string UpFormatted { get; set; } = string.Empty;

    /// <summary>
    /// 下行流量（格式化，如 "150.8GB"）
    /// </summary>
    public string DownFormatted { get; set; } = string.Empty;

    /// <summary>
    /// 上行流量（字节）
    /// </summary>
    public long UploadBytes { get; set; }

    /// <summary>
    /// 下行流量（字节）
    /// </summary>
    public long DownloadBytes { get; set; }
}

/// <summary>
/// 每日流量趋势
/// </summary>
public class DailyTrafficTrend
{
    /// <summary>
    /// 日期（格式：MM-DD）
    /// </summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>
    /// 下行流量（GB）
    /// </summary>
    public double DownloadGB { get; set; }

    /// <summary>
    /// 上行流量（GB）
    /// </summary>
    public double UploadGB { get; set; }
}

/// <summary>
/// 应用流量分布
/// </summary>
public class AppTrafficDistribution
{
    /// <summary>
    /// 应用名称
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// 总流量（GB，上行+下行）
    /// </summary>
    public double TotalGB { get; set; }

    /// <summary>
    /// 上行流量（字节）
    /// </summary>
    public long UploadBytes { get; set; }

    /// <summary>
    /// 下行流量（字节）
    /// </summary>
    public long DownloadBytes { get; set; }
}

/// <summary>
/// 设备每日流量趋势（用于单个设备查询）
/// </summary>
public class DeviceDailyTrafficDto
{
    /// <summary>
    /// 设备MAC地址
    /// </summary>
    public string Mac { get; set; } = string.Empty;

    /// <summary>
    /// 设备名称
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// 每日流量趋势
    /// </summary>
    public List<DailyTrafficTrend> DailyTrends { get; set; } = new();
}

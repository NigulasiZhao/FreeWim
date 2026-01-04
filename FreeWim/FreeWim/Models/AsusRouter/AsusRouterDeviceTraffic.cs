namespace FreeWim.Models.AsusRouter;

/// <summary>
/// 华硕路由器设备流量统计实体
/// 存储每个设备每天24小时的流量数据
/// </summary>
public class AsusRouterDeviceTraffic
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public string Id { get; set; } = null!;
    
    /// <summary>
    /// 设备MAC地址
    /// </summary>
    public string Mac { get; set; } = null!;
    
    /// <summary>
    /// 统计日期
    /// </summary>
    public DateTime StatDate { get; set; }
    
    /// <summary>
    /// 小时（0-23）
    /// </summary>
    public int Hour { get; set; }
    
    /// <summary>
    /// 上传字节数（该小时内）
    /// </summary>
    public long UploadBytes { get; set; }
    
    /// <summary>
    /// 下载字节数（该小时内）
    /// </summary>
    public long DownloadBytes { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

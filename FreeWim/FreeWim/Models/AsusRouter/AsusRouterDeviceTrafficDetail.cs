namespace FreeWim.Models.AsusRouter;

/// <summary>
/// 华硕路由器设备流量详细统计实体
/// 按应用/协议分类的流量统计
/// </summary>
public class AsusRouterDeviceTrafficDetail
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
    /// 应用/协议名称
    /// 如: Baidu, BitTorrent Series, HTTP, QUIC, SSL/TLS 等
    /// </summary>
    public string AppName { get; set; } = null!;
    
    /// <summary>
    /// 上传字节数
    /// </summary>
    public long UploadBytes { get; set; }
    
    /// <summary>
    /// 下载字节数
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

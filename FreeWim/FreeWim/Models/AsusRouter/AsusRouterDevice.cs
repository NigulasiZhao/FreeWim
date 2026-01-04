namespace FreeWim.Models.AsusRouter;

/// <summary>
/// 华硕路由器设备信息实体
/// </summary>
public class AsusRouterDevice
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public string Id { get; set; } = null!;
    
    /// <summary>
    /// MAC地址（设备唯一标识）
    /// </summary>
    public string Mac { get; set; } = null!;
    
    /// <summary>
    /// IP地址
    /// </summary>
    public string? Ip { get; set; }
    
    /// <summary>
    /// 设备名称
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// 设备昵称（自定义名称）
    /// </summary>
    public string? NickName { get; set; }
    
    /// <summary>
    /// 设备厂商
    /// </summary>
    public string? Vendor { get; set; }
    
    /// <summary>
    /// 设备厂商类别
    /// </summary>
    public string? VendorClass { get; set; }
    
    /// <summary>
    /// 设备类型
    /// </summary>
    public string? Type { get; set; }
    
    /// <summary>
    /// 默认设备类型
    /// </summary>
    public string? DefaultType { get; set; }
    
    /// <summary>
    /// 是否无线连接（0:有线, 1:2.4G WiFi, 2:5G WiFi）
    /// </summary>
    public string? IsWL { get; set; }
    
    /// <summary>
    /// 是否为网关
    /// </summary>
    public string? IsGateway { get; set; }
    
    /// <summary>
    /// 是否为Web服务器
    /// </summary>
    public string? IsWebServer { get; set; }
    
    /// <summary>
    /// 是否为打印机
    /// </summary>
    public string? IsPrinter { get; set; }
    
    /// <summary>
    /// 是否为iTunes设备
    /// </summary>
    public string? IsITunes { get; set; }
    
    /// <summary>
    /// 是否在线（1:在线, 0:离线）
    /// </summary>
    public string? IsOnline { get; set; }
    
    /// <summary>
    /// 是否登录
    /// </summary>
    public string? IsLogin { get; set; }
    
    /// <summary>
    /// SSID（WiFi名称）
    /// </summary>
    public string? Ssid { get; set; }
    
    /// <summary>
    /// 信号强度（dBm）
    /// </summary>
    public string? Rssi { get; set; }
    
    /// <summary>
    /// 当前上传速度（Mbps）
    /// </summary>
    public string? CurTx { get; set; }
    
    /// <summary>
    /// 当前下载速度（Mbps）
    /// </summary>
    public string? CurRx { get; set; }
    
    /// <summary>
    /// 总上传流量
    /// </summary>
    public string? TotalTx { get; set; }
    
    /// <summary>
    /// 总下载流量
    /// </summary>
    public string? TotalRx { get; set; }
    
    /// <summary>
    /// 无线连接时长
    /// </summary>
    public string? WlConnectTime { get; set; }
    
    /// <summary>
    /// IP获取方式（DHCP, Manual）
    /// </summary>
    public string? IpMethod { get; set; }
    
    /// <summary>
    /// 操作模式
    /// </summary>
    public string? OpMode { get; set; }
    
    /// <summary>
    /// 是否为ROG设备
    /// </summary>
    public string? ROG { get; set; }
    
    /// <summary>
    /// 设备分组
    /// </summary>
    public string? Group { get; set; }
    
    /// <summary>
    /// QoS等级
    /// </summary>
    public string? QosLevel { get; set; }
    
    /// <summary>
    /// 互联网访问模式（allow, block）
    /// </summary>
    public string? InternetMode { get; set; }
    
    /// <summary>
    /// 互联网状态
    /// </summary>
    public string? InternetState { get; set; }
    
    /// <summary>
    /// DPI类型
    /// </summary>
    public string? DpiType { get; set; }
    
    /// <summary>
    /// DPI设备
    /// </summary>
    public string? DpiDevice { get; set; }
    
    /// <summary>
    /// 是否为GN设备
    /// </summary>
    public string? IsGN { get; set; }
    
    /// <summary>
    /// MAC地址是否重复
    /// </summary>
    public string? MacRepeat { get; set; }
    
    /// <summary>
    /// 回调地址
    /// </summary>
    public string? Callback { get; set; }
    
    /// <summary>
    /// 是否保持ARP
    /// </summary>
    public string? KeepArp { get; set; }
    
    /// <summary>
    /// WTFast状态
    /// </summary>
    public string? WtFast { get; set; }
    
    /// <summary>
    /// 操作系统类型
    /// </summary>
    public int? OsType { get; set; }
    
    /// <summary>
    /// 是否为AiMesh中继器
    /// </summary>
    public string? AmeshIsRe { get; set; }
    
    /// <summary>
    /// AiMesh绑定MAC地址
    /// </summary>
    public string? AmeshBindMac { get; set; }
    
    /// <summary>
    /// AiMesh绑定频段
    /// </summary>
    public string? AmeshBindBand { get; set; }
    
    /// <summary>
    /// 数据来源（networkmapd, nmpClient）
    /// </summary>
    public string? DataSource { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

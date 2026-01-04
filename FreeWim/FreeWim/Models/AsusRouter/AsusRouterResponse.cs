namespace FreeWim.Models.AsusRouter;

/// <summary>
/// 华硕路由器API响应
/// </summary>
public class AsusRouterResponse
{
    /// <summary>
    /// 所有设备列表
    /// </summary>
    public List<AsusRouterDevice> Devices { get; set; } = new();
    
    /// <summary>
    /// 来自networkmapd的设备数量
    /// </summary>
    public int FromNetworkmapCount { get; set; }
    
    /// <summary>
    /// 来自nmpClient的设备数量
    /// </summary>
    public int FromNmpClientCount { get; set; }
    
    /// <summary>
    /// 客户端API级别
    /// </summary>
    public string? ClientAPILevel { get; set; }
    
    /// <summary>
    /// 获取在线设备
    /// </summary>
    public List<AsusRouterDevice> GetOnlineDevices()
    {
        return Devices.Where(d => d.IsOnline == "1").ToList();
    }
    
    /// <summary>
    /// 获取无线设备
    /// </summary>
    public List<AsusRouterDevice> GetWirelessDevices()
    {
        return Devices.Where(d => d.IsWL == "1" || d.IsWL == "2").ToList();
    }
    
    /// <summary>
    /// 按厂商分组
    /// </summary>
    public Dictionary<string, List<AsusRouterDevice>> GetDevicesByVendor()
    {
        return Devices
            .Where(d => !string.IsNullOrEmpty(d.Vendor))
            .GroupBy(d => d.Vendor!)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}

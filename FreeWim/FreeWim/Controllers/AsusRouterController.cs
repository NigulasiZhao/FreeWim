using System.Data;
using Dapper;
using FreeWim.Services;
using FreeWim.Models.AsusRouter;
using FreeWim.Models.AsusRouter.Dto;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AsusRouterController(
    IConfiguration configuration,
    AsusRouterService asusRouterService,
    TokenService tokenService,
    ILogger<AsusRouterController> logger)
    : Controller
{
    [Tags("华硕")]
    [EndpointSummary("获取华硕路由器token")]
    [HttpGet]
    public string? GetAsusRouterTokenAsync()
    {
        var json = tokenService.GetAsusRouterTokenAsync();
        return json;
    }

    [Tags("华硕")]
    [EndpointSummary("获取路由器连接设备")]
    [HttpGet]
    public async Task<ActionResult> GetNetworkDevicesAsync()
    {
        try
        {
            var devices = await asusRouterService.GetNetworkDevicesAsync();

            return Json(new
            {
                success = true,
                message = "获取成功",
                data = new
                {
                    totalCount = devices.Devices.Count,
                    onlineCount = devices.GetOnlineDevices().Count,
                    wirelessCount = devices.GetWirelessDevices().Count,
                    fromNetworkmapCount = devices.FromNetworkmapCount,
                    fromNmpClientCount = devices.FromNmpClientCount,
                    clientAPILevel = devices.ClientAPILevel,
                    devices = devices.Devices,
                    onlineDevices = devices.GetOnlineDevices(),
                    devicesByVendor = devices.GetDevicesByVendor()
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = $"获取失败: {ex.Message}"
            });
        }
    }

    [Tags("华硕")]
    [EndpointSummary("获取路由器连接设备并保存到数据库")]
    [HttpGet]
    public async Task<ActionResult> SyncNetworkDevicesAsync()
    {
        try
        {
            // 1. 获取设备信息
            var devices = await asusRouterService.GetNetworkDevicesAsync();

            // 2. 保存到数据库
            var savedCount = await asusRouterService.SaveDevicesToDatabaseAsync(devices);

            return Json(new
            {
                success = true,
                message = "同步成功",
                data = new
                {
                    totalCount = devices.Devices.Count,
                    savedCount,
                    onlineCount = devices.GetOnlineDevices().Count,
                    wirelessCount = devices.GetWirelessDevices().Count,
                    fromNetworkmapCount = devices.FromNetworkmapCount,
                    fromNmpClientCount = devices.FromNmpClientCount
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = $"同步失败: {ex.Message}"
            });
        }
    }

    [Tags("华硕")]
    [EndpointSummary("查询数据库中的路由器设备信息")]
    [HttpGet]
    public async Task<ActionResult> GetDevicesFromDatabase()
    {
        try
        {
            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);

            var devices = await dbConnection.QueryAsync<AsusRouterDevice>(
                "SELECT * FROM asusrouterdevice ORDER BY updatedat DESC"
            );

            var deviceList = devices.ToList();
            var onlineDevices = deviceList.Where(d => d.IsOnline == "1").ToList();
            var offlineDevices = deviceList.Where(d => d.IsOnline != "1").ToList();

            return Json(new
            {
                success = true,
                message = "查询成功",
                data = new
                {
                    totalCount = deviceList.Count,
                    onlineCount = onlineDevices.Count,
                    offlineCount = offlineDevices.Count,
                    devices = deviceList,
                    onlineDevices,
                    offlineDevices
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = $"查询失败: {ex.Message}"
            });
        }
    }

    [Tags("华硕")]
    [EndpointSummary("根据MAC地址查询设备信息")]
    [HttpGet]
    public async Task<ActionResult> GetDeviceByMac(string mac)
    {
        try
        {
            if (string.IsNullOrEmpty(mac))
                return Json(new
                {
                    success = false,
                    message = "MAC地址不能为空"
                });

            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);

            var device = await dbConnection.QueryFirstOrDefaultAsync<AsusRouterDevice>(
                "SELECT * FROM asusrouterdevice WHERE mac = @Mac",
                new { Mac = mac }
            );

            if (device == null)
                return Json(new
                {
                    success = false,
                    message = "未找到该设备"
                });

            return Json(new
            {
                success = true,
                message = "查询成功",
                data = device
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = $"查询失败: {ex.Message}"
            });
        }
    }

    [Tags("华硕")]
    [EndpointSummary("获取设备小时级流量数据（调用路由器接口）")]
    [HttpGet]
    public async Task<ActionResult> GetDeviceHourlyTraffic(string mac, string date)
    {
        try
        {
            if (string.IsNullOrEmpty(mac))
                return Json(new
                {
                    success = false,
                    message = "MAC地址不能为空"
                });

            if (string.IsNullOrEmpty(date))
                return Json(new
                {
                    success = false,
                    message = "日期不能为空，格式: yyyy-MM-dd"
                });

            if (!DateTime.TryParse(date, out var queryDate))
                return Json(new
                {
                    success = false,
                    message = "日期格式错误，请使用 yyyy-MM-dd 格式"
                });

            // 转换为Unix时间戳（秒级）
            var dateTimestamp = new DateTimeOffset(queryDate.Date).ToUnixTimeSeconds();

            // 调用路由器接口获取小时级流量数据
            var trafficData = await asusRouterService.GetDeviceTrafficAsync(mac, dateTimestamp);

            if (trafficData.Count == 0)
                return Json(new
                {
                    success = false,
                    message = $"未获取到设备 {mac} 在 {date} 的流量数据，可能设备不存在或该日期无数据"
                });

            // 计算总流量
            var totalUpload = trafficData.Sum(t => t.Upload);
            var totalDownload = trafficData.Sum(t => t.Download);

            return Json(new
            {
                success = true,
                message = "获取成功",
                data = new
                {
                    mac,
                    date,
                    totalUpload,
                    totalDownload,
                    totalUploadFormatted = totalUpload / 1073741824,
                    totalDownloadFormatted = totalDownload / 1073741824,
                    hourlyData = trafficData.Select((t, index) => new
                    {
                        hour = index,
                        timeRange = $"{index:D2}:00 - {index + 1:D2}:00",
                        uploadBytes = t.Upload,
                        downloadBytes = t.Download,
                        uploadFormatted = t.Upload / 1073741824,
                        downloadFormatted = t.Download / 1073741824
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = $"获取失败: {ex.Message}"
            });
        }
    }

    [Tags("华硕")]
    [EndpointSummary("获取设备详细流量数据（按应用/协议分类，调用路由器接口）")]
    [HttpGet]
    public async Task<ActionResult> GetDeviceDetailTraffic(string mac, string date)
    {
        try
        {
            if (string.IsNullOrEmpty(mac))
                return Json(new
                {
                    success = false,
                    message = "MAC地址不能为空"
                });

            if (string.IsNullOrEmpty(date))
                return Json(new
                {
                    success = false,
                    message = "日期不能为空，格式: yyyy-MM-dd"
                });

            if (!DateTime.TryParse(date, out var queryDate))
                return Json(new
                {
                    success = false,
                    message = "日期格式错误，请使用 yyyy-MM-dd 格式"
                });

            // 转换为Unix时间戳（秒级）
            var dateTimestamp = new DateTimeOffset(queryDate.Date).ToUnixTimeSeconds();

            // 调用路由器接口获取详细流量数据
            var trafficDetailData = await asusRouterService.GetDeviceTrafficDetailAsync(mac, dateTimestamp);

            if (trafficDetailData.Count == 0)
                return Json(new
                {
                    success = false,
                    message = $"未获取到设备 {mac} 在 {date} 的详细流量数据，可能设备不存在或该日期无数据"
                });

            // 计算总流量
            var totalUpload = trafficDetailData.Sum(t => t.Upload);
            var totalDownload = trafficDetailData.Sum(t => t.Download);

            // 计算流量占比
            double CalculatePercentage(long bytes, long total)
            {
                if (total == 0) return 0.0;
                return Math.Round((double)bytes / total * 100, 2);
            }

            // 按下载量降序排列
            var sortedData = trafficDetailData.OrderByDescending(t => t.Download).ToList();

            return Json(new
            {
                success = true,
                message = "获取成功",
                data = new
                {
                    mac,
                    date,
                    totalUpload,
                    totalDownload,
                    totalUploadFormatted = totalUpload / 1073741824,
                    totalDownloadFormatted = totalDownload / 1073741824,
                    appCount = sortedData.Count,
                    topApps = sortedData.Take(10).Select(t => new
                    {
                        appName = t.AppName,
                        uploadBytes = t.Upload,
                        downloadBytes = t.Download,
                        uploadFormatted = t.Upload / 1073741824,
                        downloadFormatted = t.Download / 1073741824,
                        uploadPercentage = CalculatePercentage(t.Upload, totalUpload),
                        downloadPercentage = CalculatePercentage(t.Download, totalDownload)
                    }).ToList(),
                    allApps = sortedData.Select(t => new
                    {
                        appName = t.AppName,
                        uploadBytes = t.Upload,
                        downloadBytes = t.Download,
                        uploadFormatted = t.Upload / 1073741824,
                        downloadFormatted = t.Download / 1073741824,
                        uploadPercentage = CalculatePercentage(t.Upload, totalUpload),
                        downloadPercentage = CalculatePercentage(t.Download, totalDownload)
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = $"获取失败: {ex.Message}"
            });
        }
    }

    [Tags("华硕")]
    [EndpointSummary("获取流量监控页面数据")]
    [HttpGet]
    public async Task<ActionResult> GetTrafficMonitoringData(string? startDate = null, string? endDate = null)
    {
        try
        {
            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);

            // 解析日期参数，默认为最近30天
            DateTime start, end;
            if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            {
                end = DateTime.Now.Date;
                start = end.AddDays(-29);
            }
            else
            {
                if (!DateTime.TryParse(startDate, out start) || !DateTime.TryParse(endDate, out end))
                    return Json(new
                    {
                        success = false,
                        message = "日期格式错误，请使用 yyyy-MM-dd 格式"
                    });
                start = start.Date;
                end = end.Date;
            }

            var result = new TrafficMonitoringDto();

            // 1. 获取设备列表（带名称）
            var devices = await dbConnection.QueryAsync<AsusRouterDevice>(@"
                SELECT DISTINCT ON (mac) mac, name, nickname, type, updatedat 
                FROM asusrouterdevice 
                ORDER BY mac, updatedat DESC
            ");
            var deviceList = devices.ToList();

            // 2. 查询指定日期范围内的流量数据（按设备汇总）
            var deviceTrafficData = await dbConnection.QueryAsync<dynamic>(@"
                SELECT 
                    mac,
                    SUM(uploadbytes) as total_upload,
                    SUM(downloadbytes) as total_download
                FROM asusrouterdevicetraffic
                WHERE statdate BETWEEN @StartDate AND @EndDate
                GROUP BY mac
                ORDER BY SUM(downloadbytes) DESC
            ", new { StartDate = start, EndDate = end });

            var deviceTrafficList = deviceTrafficData.ToList();

            // 3. 计算总流量和KPI
            long totalUpload = 0;
            long totalDownload = 0;
            foreach (var dt in deviceTrafficList)
            {
                totalUpload += (long)dt.total_upload;
                totalDownload += (long)dt.total_download;
            }

            var dayCount = (end - start).Days + 1;
            result.Kpi = new KpiStatistics
            {
                TotalUploadBytes = totalUpload,
                TotalDownloadBytes = totalDownload,
                TotalUploadFormatted = FormatBytes(totalUpload),
                TotalDownloadFormatted = FormatBytes(totalDownload),
                AvgDailyUpload = FormatBytes(dayCount > 0 ? totalUpload / dayCount : 0),
                AvgDailyDownload = FormatBytes(dayCount > 0 ? totalDownload / dayCount : 0),
                DayCount = dayCount
            };

            // 4. 构建设备列表（添加"所有设备"选项）
            result.Devices.Add(new DeviceTrafficSummary
            {
                Id = "all",
                Name = "所有设备",
                Icon = "🌐",
                UploadBytes = totalUpload,
                DownloadBytes = totalDownload,
                UpFormatted = FormatBytes(totalUpload),
                DownFormatted = FormatBytes(totalDownload)
            });

            foreach (var dt in deviceTrafficList)
            {
                var device = deviceList.FirstOrDefault(d => d.Mac == dt.mac);
                var deviceName = device?.NickName ?? device?.Name ?? dt.mac;
                var icon = GetDeviceIcon(device?.Type);

                result.Devices.Add(new DeviceTrafficSummary
                {
                    Id = dt.mac,
                    Name = deviceName,
                    Icon = icon,
                    UploadBytes = (long)dt.total_upload,
                    DownloadBytes = (long)dt.total_download,
                    UpFormatted = FormatBytes((long)dt.total_upload),
                    DownFormatted = FormatBytes((long)dt.total_download)
                });
            }

            // 5. 查询每日流量趋势（所有设备汇总）
            var dailyTrafficData = await dbConnection.QueryAsync<dynamic>(@"
                SELECT 
                    statdate,
                    SUM(uploadbytes) as daily_upload,
                    SUM(downloadbytes) as daily_download
                FROM asusrouterdevicetraffic
                WHERE statdate BETWEEN @StartDate AND @EndDate
                GROUP BY statdate
                ORDER BY statdate
            ", new { StartDate = start, EndDate = end });

            foreach (var daily in dailyTrafficData)
            {
                DateTime date = daily.statdate;
                result.DailyTrends.Add(new DailyTrafficTrend
                {
                    Date = date.ToString("MM-dd"),
                    UploadGB = Math.Round((long)daily.daily_upload / 1073741824.0, 2),
                    DownloadGB = Math.Round((long)daily.daily_download / 1073741824.0, 2)
                });
            }

            // 6. 查询应用流量分布（选定周期内，Top 10，排除General项）
            var appTrafficData = await dbConnection.QueryAsync<dynamic>(@"
                SELECT 
                    appname,
                    SUM(uploadbytes) as app_upload,
                    SUM(downloadbytes) as app_download
                FROM asusrouterdevicetrafficdetail
                WHERE statdate BETWEEN @StartDate AND @EndDate
                    AND appname NOT IN ('General', 'UNKNOWN', 'Unknown', 'Other')
                GROUP BY appname
                HAVING SUM(uploadbytes) + SUM(downloadbytes) > 0
                ORDER BY SUM(downloadbytes) + SUM(uploadbytes) DESC
                LIMIT 10
            ", new { StartDate = start, EndDate = end });

            foreach (var app in appTrafficData)
            {
                var upload = (long)app.app_upload;
                var download = (long)app.app_download;
                result.AppDistributions.Add(new AppTrafficDistribution
                {
                    AppName = app.appname,
                    UploadBytes = upload,
                    DownloadBytes = download,
                    TotalGB = Math.Round((upload + download) / 1073741824.0, 2)
                });
            }

            return Json(new
            {
                success = true,
                message = "获取成功",
                data = result
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取流量监控数据失败");
            return Json(new
            {
                success = false,
                message = $"获取失败: {ex.Message}"
            });
        }
    }

    [Tags("华硕")]
    [EndpointSummary("获取单个设备的每日流量趋势")]
    [HttpGet]
    public async Task<ActionResult> GetDeviceDailyTraffic(string mac, string? startDate = null, string? endDate = null)
    {
        try
        {
            if (string.IsNullOrEmpty(mac))
                return Json(new
                {
                    success = false,
                    message = "MAC地址不能为空"
                });

            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);

            // 解析日期参数
            DateTime start, end;
            if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            {
                end = DateTime.Now.Date;
                start = end.AddDays(-29);
            }
            else
            {
                if (!DateTime.TryParse(startDate, out start) || !DateTime.TryParse(endDate, out end))
                    return Json(new
                    {
                        success = false,
                        message = "日期格式错误，请使用 yyyy-MM-dd 格式"
                    });
                start = start.Date;
                end = end.Date;
            }

            // 获取设备信息
            var device = await dbConnection.QueryFirstOrDefaultAsync<AsusRouterDevice>(
                "SELECT * FROM asusrouterdevice WHERE mac = @Mac LIMIT 1",
                new { Mac = mac }
            );

            var deviceName = device?.NickName ?? device?.Name ?? mac;

            // 查询该设备的每日流量趋势
            var dailyTrafficData = await dbConnection.QueryAsync<dynamic>(@"
                SELECT 
                    statdate,
                    SUM(uploadbytes) as daily_upload,
                    SUM(downloadbytes) as daily_download
                FROM asusrouterdevicetraffic
                WHERE mac = @Mac AND statdate BETWEEN @StartDate AND @EndDate
                GROUP BY statdate
                ORDER BY statdate
            ", new { Mac = mac, StartDate = start, EndDate = end });

            var result = new DeviceDailyTrafficDto
            {
                Mac = mac,
                DeviceName = deviceName
            };

            foreach (var daily in dailyTrafficData)
            {
                DateTime date = daily.statdate;
                result.DailyTrends.Add(new DailyTrafficTrend
                {
                    Date = date.ToString("MM-dd"),
                    UploadGB = Math.Round((long)daily.daily_upload / 1073741824.0, 2),
                    DownloadGB = Math.Round((long)daily.daily_download / 1073741824.0, 2)
                });
            }

            return Json(new
            {
                success = true,
                message = "获取成功",
                data = result
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取设备流量趋势失败");
            return Json(new
            {
                success = false,
                message = $"获取失败: {ex.Message}"
            });
        }
    }

    [Tags("华硕")]
    [EndpointSummary("获取流量占比数据 - 所有设备返回每日占比，单个设备返回时段占比")]
    [HttpGet]
    public async Task<ActionResult> GetTrafficDistribution(string? deviceId = "all", string? startDate = null, string? endDate = null)
    {
        try
        {
            using IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);

            // 解析日期参数
            DateTime start, end;
            if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            {
                end = DateTime.Now.Date;
                start = end.AddDays(-14);
            }
            else
            {
                if (!DateTime.TryParse(startDate, out start) || !DateTime.TryParse(endDate, out end))
                    return Json(new
                    {
                        success = false,
                        message = "日期格式错误，请使用 yyyy-MM-dd 格式"
                    });
                start = start.Date;
                end = end.Date;
            }

            // 所有设备：返回24小时时段流量占比（汇总所有设备）
            if (deviceId == "all")
            {
                var hourlyTrafficData = await dbConnection.QueryAsync<dynamic>(@"
                    SELECT 
                        hour,
                        SUM(uploadbytes) as hour_upload,
                        SUM(downloadbytes) as hour_download
                    FROM asusrouterdevicetraffic
                    WHERE statdate BETWEEN @StartDate AND @EndDate
                    GROUP BY hour
                    ORDER BY hour
                ", new { StartDate = start, EndDate = end });

                var hourlyList = hourlyTrafficData.ToList();
                var totalBytes = hourlyList.Sum(h => (long)h.hour_upload + (long)h.hour_download);

                var hourlyDistributions = hourlyList.Select(h =>
                {
                    var hourTotal = (long)h.hour_upload + (long)h.hour_download;
                    var hour = (int)h.hour;
                    return new
                    {
                        name = $"{hour:D2}:00",
                        hour,
                        value = Math.Round(hourTotal / 1073741824.0, 2), // GB
                        percentage = totalBytes > 0 ? Math.Round((double)hourTotal / totalBytes * 100, 2) : 0
                    };
                }).ToList();

                return Json(new
                {
                    success = true,
                    message = "获取成功",
                    data = new
                    {
                        type = "hourly",
                        title = "所有设备：时段流量占比",
                        distributions = hourlyDistributions,
                        totalGB = Math.Round(totalBytes / 1073741824.0, 2)
                    }
                });
            }
            // 单个设备：返回时段流量占比（24小时）
            else
            {
                var hourlyTrafficData = await dbConnection.QueryAsync<dynamic>(@"
                    SELECT 
                        hour,
                        SUM(uploadbytes) as hour_upload,
                        SUM(downloadbytes) as hour_download
                    FROM asusrouterdevicetraffic
                    WHERE mac = @Mac AND statdate BETWEEN @StartDate AND @EndDate
                    GROUP BY hour
                    ORDER BY hour
                ", new { Mac = deviceId, StartDate = start, EndDate = end });

                var hourlyList = hourlyTrafficData.ToList();
                var totalBytes = hourlyList.Sum(h => (long)h.hour_upload + (long)h.hour_download);

                // 获取设备名称
                var device = await dbConnection.QueryFirstOrDefaultAsync<AsusRouterDevice>(
                    "SELECT * FROM asusrouterdevice WHERE mac = @Mac LIMIT 1",
                    new { Mac = deviceId }
                );
                var deviceName = device?.NickName ?? device?.Name ?? deviceId;

                var hourlyDistributions = hourlyList.Select(h =>
                {
                    var hourTotal = (long)h.hour_upload + (long)h.hour_download;
                    var hour = (int)h.hour;
                    return new
                    {
                        name = $"{hour:D2}:00",
                        hour,
                        value = Math.Round(hourTotal / 1073741824.0, 2), // GB
                        percentage = totalBytes > 0 ? Math.Round((double)hourTotal / totalBytes * 100, 2) : 0
                    };
                }).ToList();

                return Json(new
                {
                    success = true,
                    message = "获取成功",
                    data = new
                    {
                        type = "hourly",
                        title = $"{deviceName}：时段流量占比",
                        distributions = hourlyDistributions,
                        totalGB = Math.Round(totalBytes / 1073741824.0, 2)
                    }
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取流量占比数据失败");
            return Json(new
            {
                success = false,
                message = $"获取失败: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// 格式化字节数为友好显示
    /// </summary>
    private string FormatBytes(long bytes)
    {
        const long gb = 1073741824;
        const long tb = 1099511627776;

        if (bytes >= tb)
            return $"{Math.Round(bytes / (double)tb, 2)}TB";
        else if (bytes >= gb)
            return $"{Math.Round(bytes / (double)gb, 2)}GB";
        else if (bytes >= 1048576)
            return $"{Math.Round(bytes / 1048576.0, 2)}MB";
        else
            return $"{Math.Round(bytes / 1024.0, 2)}KB";
    }

    /// <summary>
    /// 根据设备类型获取图标
    /// </summary>
    private string GetDeviceIcon(string? deviceType)
    {
        if (string.IsNullOrEmpty(deviceType))
            return "📱";

        return deviceType.ToLower() switch
        {
            var t when t.Contains("phone") || t.Contains("mobile") => "📱",
            var t when t.Contains("laptop") || t.Contains("notebook") || t.Contains("macbook") => "💻",
            var t when t.Contains("desktop") || t.Contains("pc") => "🖥️",
            var t when t.Contains("tv") || t.Contains("television") => "📺",
            var t when t.Contains("nas") || t.Contains("storage") => "💾",
            var t when t.Contains("game") || t.Contains("console") || t.Contains("ps") || t.Contains("xbox") => "🎮",
            var t when t.Contains("tablet") || t.Contains("ipad") => "📱",
            var t when t.Contains("watch") => "⌚",
            var t when t.Contains("router") || t.Contains("gateway") => "🌐",
            var t when t.Contains("camera") => "📷",
            var t when t.Contains("printer") => "🖨️",
            _ => "📱"
        };
    }
}
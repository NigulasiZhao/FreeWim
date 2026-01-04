using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FreeWim.Common;
using FreeWim.Models.AsusRouter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AsusRouterController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly AsusRouterHelper _asusRouterHelper;
    private readonly TokenService _tokenService;
    private readonly ILogger<AsusRouterController> _logger;

    public AsusRouterController(
        IConfiguration configuration,
        AsusRouterHelper asusRouterHelper,
        TokenService tokenService,
        ILogger<AsusRouterController> logger)
    {
        _configuration = configuration;
        _asusRouterHelper = asusRouterHelper;
        _tokenService = tokenService;
        _logger = logger;
    }

    [Tags("华硕")]
    [EndpointSummary("获取华硕路由器token")]
    [HttpGet]
    public string? GetAsusRouterTokenAsync()
    {
        var json = _tokenService.GetAsusRouterTokenAsync();
        return json;
    }

    [Tags("华硕")]
    [EndpointSummary("获取路由器连接设备")]
    [HttpGet]
    public async Task<ActionResult> GetNetworkDevicesAsync()
    {
        try
        {
            _logger.LogInformation("开始获取网络设备列表...");
            var devices = await _asusRouterHelper.GetNetworkDevicesAsync();

            _logger.LogInformation($"总设备数: {devices.Devices.Count}");
            _logger.LogInformation($"在线设备: {devices.GetOnlineDevices().Count}");
            _logger.LogInformation($"无线设备: {devices.GetWirelessDevices().Count}");
            _logger.LogInformation($"networkmapd设备: {devices.FromNetworkmapCount}");
            _logger.LogInformation($"nmpClient设备: {devices.FromNmpClientCount}");

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
            _logger.LogError(ex, "获取网络设备列表失败");
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
            _logger.LogInformation("开始同步网络设备列表到数据库...");
            
            // 1. 获取设备信息
            var devices = await _asusRouterHelper.GetNetworkDevicesAsync();
            
            _logger.LogInformation($"获取到 {devices.Devices.Count} 个设备");

            // 2. 保存到数据库
            var savedCount = await _asusRouterHelper.SaveDevicesToDatabaseAsync(devices);

            _logger.LogInformation($"成功同步 {savedCount} 个设备到数据库");

            return Json(new
            {
                success = true,
                message = "同步成功",
                data = new
                {
                    totalCount = devices.Devices.Count,
                    savedCount = savedCount,
                    onlineCount = devices.GetOnlineDevices().Count,
                    wirelessCount = devices.GetWirelessDevices().Count,
                    fromNetworkmapCount = devices.FromNetworkmapCount,
                    fromNmpClientCount = devices.FromNmpClientCount
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步网络设备列表失败");
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
            using IDbConnection dbConnection = new NpgsqlConnection(_configuration["Connection"]);
            
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
                    onlineDevices = onlineDevices,
                    offlineDevices = offlineDevices
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询数据库设备信息失败");
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
            {
                return Json(new
                {
                    success = false,
                    message = "MAC地址不能为空"
                });
            }

            using IDbConnection dbConnection = new NpgsqlConnection(_configuration["Connection"]);
            
            var device = await dbConnection.QueryFirstOrDefaultAsync<AsusRouterDevice>(
                "SELECT * FROM asusrouterdevice WHERE mac = @Mac",
                new { Mac = mac }
            );

            if (device == null)
            {
                return Json(new
                {
                    success = false,
                    message = "未找到该设备"
                });
            }

            return Json(new
            {
                success = true,
                message = "查询成功",
                data = device
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"查询设备信息失败，MAC: {mac}");
            return Json(new
            {
                success = false,
                message = $"查询失败: {ex.Message}"
            });
        }
    }
}
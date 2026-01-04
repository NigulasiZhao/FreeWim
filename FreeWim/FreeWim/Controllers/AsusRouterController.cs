using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FreeWim.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AsusRouterController(IConfiguration configuration, AsusRouterHelper asusRouterHelper, TokenService tokenService) : Controller
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
    public ActionResult GetNetworkDevicesAsync(string[] args)
    {
        try
        {
            Console.WriteLine("正在获取网络设备列表...");
            var devices = asusRouterHelper.GetNetworkDevicesAsync().Result;

            Console.WriteLine($"\n=== 网络设备统计 ===");
            Console.WriteLine($"总设备数: {devices.GetAllDevices().Count()}");
            Console.WriteLine($"在线设备: {devices.GetOnlineDevices().Count()}");
            Console.WriteLine($"无线设备: {devices.GetWirelessDevices().Count()}");
            Console.WriteLine($"networkmapd设备: {devices.FromNetworkmapdDevices.Count}");
            Console.WriteLine($"nmpClient设备: {devices.NmpClientDevices.Count}");

            Console.WriteLine($"\n=== 在线设备列表 ===");
            foreach (var device in devices.GetOnlineDevices().OrderBy(d => d.Ip))
            {
                Console.WriteLine($"- {device.DisplayName}");
                Console.WriteLine($"  IP: {device.Ip}, MAC: {device.Mac}");
                Console.WriteLine($"  类型: {device.ConnectionType}, 厂商: {device.Vendor}");
                if (device.IsWireless != "0")
                {
                    Console.WriteLine($"  信号强度: {device.Rssi}dBm, 连接时间: {device.ConnectTime}");
                    Console.WriteLine($"  上行: {device.CurrentTx}Mbps, 下行: {device.CurrentRx}Mbps");
                }

                Console.WriteLine();
            }

            Console.WriteLine($"\n=== 按厂商分组 ===");
            var vendors = devices.GetDevicesByVendor();
            foreach (var vendor in vendors.OrderByDescending(v => v.Value.Count)) Console.WriteLine($"{vendor.Key}: {vendor.Value.Count}个设备");
            Console.WriteLine($"\n数据已保存到 network_devices.json");
            return Json(devices);
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FreeWim.Common;

public class AsusRouterHelper(IConfiguration configuration, TokenService tokenService)
{
    public async Task<NetworkDevicesResponse> GetNetworkDevicesAsync()
    {
        var baseUrl = configuration.GetValue<string>("AsusRouter:RouterIp", "192.168.50.1");
        var httpClient = new HttpClient();
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01",
            ["Accept-Language"] = "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6",
            ["Connection"] = "keep-alive",
            ["Referer"] = $"{baseUrl}/device-map/clients.asp",
            ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0",
            ["X-Requested-With"] = "XMLHttpRequest"
        };
        headers["Cookie"] = $"hwaddr=7C:10:C9:E8:6D:C8; apps_last=; bw_rtab=WIRED; maxBandwidth=100; asus_token={tokenService.GetAsusRouterTokenAsync()}; clickedItem_tab=0";
        try
        {
            var url = $"{baseUrl}/update_clients.asp?={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return ParseResponse(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            throw new Exception($"获取网络设备列表失败: {ex.Message}", ex);
        }
    }

    private NetworkDevicesResponse ParseResponse(string responseContent)
    {
        try
        {
            var root = JsonDocument.Parse(ExtractJsonData(responseContent)).RootElement;
            var result = new NetworkDevicesResponse();

            var resultClientApiLevel = result.ClientAPILevel;
            ParseDeviceSource(root, "fromNetworkmapd", "networkmapd", result.FromNetworkmapdDevices, ref resultClientApiLevel);
            var resultNmpClientApiLevel = result.NmpClientAPILevel;
            ParseDeviceSource(root, "nmpClient", "nmpClient", result.NmpClientDevices, ref resultNmpClientApiLevel);

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"解析响应数据失败: {ex.Message}", ex);
        }
    }

    private void ParseDeviceSource(JsonElement root, string propertyName, string sourceName,
        Dictionary<string, NetworkDevice> devices, ref string apiLevel)
    {
        if (!root.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() == 0) return;

        var element = array[0];

        if (element.TryGetProperty("maclist", out var macList) && macList.ValueKind == JsonValueKind.Array)
            foreach (var mac in macList.EnumerateArray())
            {
                var macString = mac.GetString();
                if (!string.IsNullOrEmpty(macString) && element.TryGetProperty(macString, out var deviceElement))
                    devices[macString] = ParseDevice(deviceElement, sourceName);
            }

        if (element.TryGetProperty("ClientAPILevel", out var level))
            apiLevel = level.GetString();
    }

    private string ExtractJsonData(string responseContent)
    {
        var lines = responseContent.Split('\n');
        var dataLines = new List<string>();
        var inData = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("originData = {") || trimmed.StartsWith("originData : {"))
            {
                inData = true;
                dataLines.Add("{");
            }
            else if (inData)
            {
                dataLines.Add(trimmed);
                if (trimmed.EndsWith("}")) break;
            }
        }

        var jsonString = string.Join("", dataLines);
        return jsonString.EndsWith(";") ? jsonString[..^1] : jsonString;
    }

    private NetworkDevice ParseDevice(JsonElement element, string source)
    {
        return new NetworkDevice
        {
            Source = source,
            Mac = GetStringProperty(element, "mac"),
            Ip = GetStringProperty(element, "ip"),
            Name = GetStringProperty(element, "name"),
            NickName = GetStringProperty(element, "nickName"),
            Vendor = GetStringProperty(element, "vendor"),
            Type = GetStringProperty(element, "type"),
            DefaultType = GetStringProperty(element, "defaultType"),
            IsWireless = GetStringProperty(element, "isWL"),
            IsOnline = GetStringProperty(element, "isOnline") == "1",
            Rssi = GetStringProperty(element, "rssi"),
            CurrentTx = GetStringProperty(element, "curTx"),
            CurrentRx = GetStringProperty(element, "curRx"),
            Ssid = GetStringProperty(element, "ssid"),
            ConnectTime = GetStringProperty(element, "wlConnectTime"),
            IpMethod = GetStringProperty(element, "ipMethod"),
            InternetMode = GetStringProperty(element, "internetMode")
        };
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }
}

public class NetworkDevicesResponse
{
    public Dictionary<string, NetworkDevice> FromNetworkmapdDevices { get; set; } = new();
    public Dictionary<string, NetworkDevice> NmpClientDevices { get; set; } = new();
    public string ClientAPILevel { get; set; }
    public string NmpClientAPILevel { get; set; }

    // 获取所有设备（合并两个来源）
    public IEnumerable<NetworkDevice> GetAllDevices()
    {
        var allDevices = new List<NetworkDevice>();
        allDevices.AddRange(FromNetworkmapdDevices.Values);
        allDevices.AddRange(NmpClientDevices.Values);
        return allDevices;
    }

    // 获取在线设备
    public IEnumerable<NetworkDevice> GetOnlineDevices()
    {
        return GetAllDevices().Where(d => d.IsOnline);
    }

    // 获取无线设备
    public IEnumerable<NetworkDevice> GetWirelessDevices()
    {
        return GetAllDevices().Where(d => d.IsWireless == "1" || d.IsWireless == "2");
    }

    // 按厂商分组
    public Dictionary<string, List<NetworkDevice>> GetDevicesByVendor()
    {
        return GetAllDevices()
            .Where(d => !string.IsNullOrEmpty(d.Vendor))
            .GroupBy(d => d.Vendor)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}

public class NetworkDevice
{
    public string Source { get; set; }
    public string Mac { get; set; }
    public string Ip { get; set; }
    public string Name { get; set; }
    public string NickName { get; set; }
    public string Vendor { get; set; }
    public string Type { get; set; }
    public string DefaultType { get; set; }
    public string IsWireless { get; set; } // 0:有线, 1:2.4G, 2:5G
    public bool IsOnline { get; set; }
    public string Rssi { get; set; }
    public string CurrentTx { get; set; } // 当前上传速度 Mbps
    public string CurrentRx { get; set; } // 当前下载速度 Mbps
    public string Ssid { get; set; }
    public string ConnectTime { get; set; }
    public string IpMethod { get; set; } // DHCP, Manual
    public string InternetMode { get; set; } // allow, block

    public string DisplayName => !string.IsNullOrEmpty(NickName) ? NickName :
        !string.IsNullOrEmpty(Name) ? Name :
        !string.IsNullOrEmpty(Mac) ? Mac : "Unknown";

    public string ConnectionType
    {
        get
        {
            return IsWireless switch
            {
                "0" => "有线",
                "1" => "2.4G WiFi",
                "2" => "5G WiFi",
                _ => "未知"
            };
        }
    }

    public override string ToString()
    {
        return $"{DisplayName} ({Ip}) - {Vendor} - {ConnectionType} - {(IsOnline ? "在线" : "离线")}";
    }
}
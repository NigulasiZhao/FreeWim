using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using FreeWim.Models.AsusRouter;
using Npgsql;

namespace FreeWim.Common;

/// <summary>
/// 华硕路由器助手类
/// 用于获取路由器连接设备信息并存储到数据库
/// </summary>
public class AsusRouterHelper
{
    private readonly IConfiguration _configuration;
    private readonly TokenService _tokenService;
    private readonly ILogger<AsusRouterHelper> _logger;
    private readonly HttpClient _httpClient;

    public AsusRouterHelper(IConfiguration configuration, TokenService tokenService, ILogger<AsusRouterHelper> logger)
    {
        _configuration = configuration;
        _tokenService = tokenService;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// 获取路由器连接的所有设备信息
    /// </summary>
    public async Task<AsusRouterResponse> GetNetworkDevicesAsync()
    {
        try
        {
            var baseUrl = _configuration.GetValue<string>("AsusRouter:RouterIp", "192.168.50.1");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = $"http://{baseUrl}/update_clients.asp?_={timestamp}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            // 设置请求头
            request.Headers.TryAddWithoutValidation("Accept", "text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01");
            request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Referer", $"http://{baseUrl}/device-map/clients.asp");
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            
            // 设置Cookie
            var token = _tokenService.GetAsusRouterTokenAsync();
            var cookie = $"hwaddr=7C:10:C9:E8:6D:C8; apps_last=; bw_rtab=WIRED; maxBandwidth=100; asus_token={token}; clickedItem_tab=0";
            request.Headers.TryAddWithoutValidation("Cookie", cookie);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"成功获取路由器设备信息，内容长度: {content.Length}");
            
            return ParseResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网络设备列表失败");
            throw new Exception($"获取网络设备列表失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 保存设备信息到数据库
    /// </summary>
    public async Task<int> SaveDevicesToDatabaseAsync(AsusRouterResponse response)
    {
        using IDbConnection dbConnection = new NpgsqlConnection(_configuration["Connection"]);
        
        try
        {
            var now = DateTime.Now;
            var savedCount = 0;

            foreach (var device in response.Devices)
            {
                // 检查设备是否已存在（根据MAC地址）
                var exists = await dbConnection.QueryFirstOrDefaultAsync<int>(
                    "SELECT COUNT(1) FROM asusrouterdevice WHERE mac = @Mac",
                    new { device.Mac }
                );

                if (exists > 0)
                {
                    // 更新现有设备信息
                    var updateSql = @"
                        UPDATE asusrouterdevice SET
                            ip = @Ip, name = @Name, nickname = @NickName, vendor = @Vendor,
                            vendorclass = @VendorClass, type = @Type, defaulttype = @DefaultType,
                            iswl = @IsWL, isgateway = @IsGateway, iswebserver = @IsWebServer,
                            isprinter = @IsPrinter, isitunes = @IsITunes, isonline = @IsOnline,
                            islogin = @IsLogin, ssid = @Ssid, rssi = @Rssi,
                            curtx = @CurTx, currx = @CurRx, totaltx = @TotalTx, totalrx = @TotalRx,
                            wlconnecttime = @WlConnectTime, ipmethod = @IpMethod, opmode = @OpMode,
                            rog = @ROG, groupname = @Group, qoslevel = @QosLevel,
                            internetmode = @InternetMode, internetstate = @InternetState,
                            dpitype = @DpiType, dpidevice = @DpiDevice, isgn = @IsGN,
                            macrepeat = @MacRepeat, callback = @Callback, keeparp = @KeepArp,
                            wtfast = @WtFast, ostype = @OsType, ameshisre = @AmeshIsRe,
                            ameshbindmac = @AmeshBindMac, ameshbindband = @AmeshBindBand,
                            datasource = @DataSource, updatedat = @UpdatedAt
                        WHERE mac = @Mac";
                    
                    device.UpdatedAt = now;
                    await dbConnection.ExecuteAsync(updateSql, device);
                }
                else
                {
                    // 插入新设备
                    var insertSql = @"
                        INSERT INTO asusrouterdevice (
                            id, mac, ip, name, nickname, vendor, vendorclass, type, defaulttype,
                            iswl, isgateway, iswebserver, isprinter, isitunes, isonline, islogin,
                            ssid, rssi, curtx, currx, totaltx, totalrx, wlconnecttime, ipmethod,
                            opmode, rog, groupname, qoslevel, internetmode, internetstate,
                            dpitype, dpidevice, isgn, macrepeat, callback, keeparp, wtfast,
                            ostype, ameshisre, ameshbindmac, ameshbindband, datasource,
                            createdat, updatedat
                        ) VALUES (
                            @Id, @Mac, @Ip, @Name, @NickName, @Vendor, @VendorClass, @Type, @DefaultType,
                            @IsWL, @IsGateway, @IsWebServer, @IsPrinter, @IsITunes, @IsOnline, @IsLogin,
                            @Ssid, @Rssi, @CurTx, @CurRx, @TotalTx, @TotalRx, @WlConnectTime, @IpMethod,
                            @OpMode, @ROG, @Group, @QosLevel, @InternetMode, @InternetState,
                            @DpiType, @DpiDevice, @IsGN, @MacRepeat, @Callback, @KeepArp, @WtFast,
                            @OsType, @AmeshIsRe, @AmeshBindMac, @AmeshBindBand, @DataSource,
                            @CreatedAt, @UpdatedAt
                        )";
                    
                    device.Id = Guid.NewGuid().ToString();
                    device.CreatedAt = now;
                    device.UpdatedAt = now;
                    await dbConnection.ExecuteAsync(insertSql, device);
                }
                
                savedCount++;
            }

            _logger.LogInformation($"成功保存 {savedCount} 个设备信息到数据库");
            return savedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存设备信息到数据库失败");
            throw new Exception($"保存设备信息到数据库失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 解析响应内容
    /// </summary>
    private AsusRouterResponse ParseResponse(string responseContent)
    {
        try
        {
            var jsonData = ExtractJsonData(responseContent);
            var root = JsonDocument.Parse(jsonData).RootElement;
            
            var response = new AsusRouterResponse();
            var allDevices = new List<AsusRouterDevice>();

            // 解析 fromNetworkmapd
            if (root.TryGetProperty("fromNetworkmapd", out var fromNetworkmapd))
            {
                var devices = ParseDeviceSource(fromNetworkmapd, "networkmapd");
                allDevices.AddRange(devices);
                response.FromNetworkmapCount = devices.Count;
                
                // 获取ClientAPILevel
                if (fromNetworkmapd.ValueKind == JsonValueKind.Array && fromNetworkmapd.GetArrayLength() > 0)
                {
                    var firstElement = fromNetworkmapd[0];
                    if (firstElement.TryGetProperty("ClientAPILevel", out var apiLevel))
                    {
                        response.ClientAPILevel = apiLevel.GetString();
                    }
                }
            }

            // 解析 nmpClient
            if (root.TryGetProperty("nmpClient", out var nmpClient))
            {
                var devices = ParseDeviceSource(nmpClient, "nmpClient");
                allDevices.AddRange(devices);
                response.FromNmpClientCount = devices.Count;
            }

            response.Devices = allDevices;
            
            _logger.LogInformation($"解析完成: 总设备数={allDevices.Count}, networkmapd={response.FromNetworkmapCount}, nmpClient={response.FromNmpClientCount}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析响应数据失败");
            throw new Exception($"解析响应数据失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从数组中解析设备列表
    /// </summary>
    private List<AsusRouterDevice> ParseDeviceSource(JsonElement arrayElement, string dataSource)
    {
        var devices = new List<AsusRouterDevice>();
        
        if (arrayElement.ValueKind != JsonValueKind.Array || arrayElement.GetArrayLength() == 0)
        {
            return devices;
        }

        var element = arrayElement[0];
        
        // 获取MAC地址列表
        if (element.TryGetProperty("maclist", out var macList) && macList.ValueKind == JsonValueKind.Array)
        {
            foreach (var mac in macList.EnumerateArray())
            {
                var macString = mac.GetString();
                if (!string.IsNullOrEmpty(macString) && element.TryGetProperty(macString, out var deviceElement))
                {
                    var device = ParseDevice(deviceElement, dataSource);
                    devices.Add(device);
                }
            }
        }

        return devices;
    }

    /// <summary>
    /// 解析单个设备信息
    /// </summary>
    private AsusRouterDevice ParseDevice(JsonElement element, string dataSource)
    {
        return new AsusRouterDevice
        {
            Mac = GetStringProperty(element, "mac") ?? string.Empty,
            Ip = GetStringProperty(element, "ip"),
            Name = GetStringProperty(element, "name"),
            NickName = GetStringProperty(element, "nickName"),
            Vendor = GetStringProperty(element, "vendor"),
            VendorClass = GetStringProperty(element, "vendorclass"),
            Type = GetStringProperty(element, "type"),
            DefaultType = GetStringProperty(element, "defaultType"),
            IsWL = GetStringProperty(element, "isWL"),
            IsGateway = GetStringProperty(element, "isGateway"),
            IsWebServer = GetStringProperty(element, "isWebServer"),
            IsPrinter = GetStringProperty(element, "isPrinter"),
            IsITunes = GetStringProperty(element, "isITunes"),
            IsOnline = GetStringProperty(element, "isOnline"),
            IsLogin = GetStringProperty(element, "isLogin"),
            Ssid = GetStringProperty(element, "ssid"),
            Rssi = GetStringProperty(element, "rssi"),
            CurTx = GetStringProperty(element, "curTx"),
            CurRx = GetStringProperty(element, "curRx"),
            TotalTx = GetStringProperty(element, "totalTx"),
            TotalRx = GetStringProperty(element, "totalRx"),
            WlConnectTime = GetStringProperty(element, "wlConnectTime"),
            IpMethod = GetStringProperty(element, "ipMethod"),
            OpMode = GetStringProperty(element, "opMode"),
            ROG = GetStringProperty(element, "ROG"),
            Group = GetStringProperty(element, "group"),
            QosLevel = GetStringProperty(element, "qosLevel"),
            InternetMode = GetStringProperty(element, "internetMode"),
            InternetState = GetStringProperty(element, "internetState"),
            DpiType = GetStringProperty(element, "dpiType"),
            DpiDevice = GetStringProperty(element, "dpiDevice"),
            IsGN = GetStringProperty(element, "isGN"),
            MacRepeat = GetStringProperty(element, "macRepeat"),
            Callback = GetStringProperty(element, "callback"),
            KeepArp = GetStringProperty(element, "keeparp"),
            WtFast = GetStringProperty(element, "wtfast"),
            OsType = GetIntProperty(element, "os_type"),
            AmeshIsRe = GetStringProperty(element, "amesh_isRe"),
            AmeshBindMac = GetStringProperty(element, "amesh_bind_mac"),
            AmeshBindBand = GetStringProperty(element, "amesh_bind_band"),
            DataSource = dataSource
        };
    }

    /// <summary>
    /// 提取JavaScript中的JSON数据
    /// </summary>
    private string ExtractJsonData(string responseContent)
    {
        var lines = responseContent.Split('\n');
        var dataLines = new List<string>();
        var inData = false;
        var braceCount = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // 找到 originData = { 开始
            if (trimmed.StartsWith("originData = {"))
            {
                inData = true;
                dataLines.Add("{");
                braceCount = 1;
                continue;
            }

            if (inData)
            {
                // 移除尾部分号
                if (trimmed.EndsWith(";"))
                {
                    trimmed = trimmed[..^1].Trim();
                }
                
                // 计算大括号数量
                braceCount += trimmed.Count(c => c == '{');
                braceCount -= trimmed.Count(c => c == '}');
                
                dataLines.Add(trimmed);
                
                // 当大括号完全匹配时结束
                if (braceCount == 0)
                {
                    break;
                }
            }
        }

        var jsonString = string.Join("", dataLines);
        _logger.LogDebug($"提取的JSON数据长度: {jsonString.Length}");
        return jsonString;
    }

    /// <summary>
    /// 获取字符串属性
    /// </summary>
    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        }
        return null;
    }

    /// <summary>
    /// 获取整数属性
    /// </summary>
    private static int? GetIntProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
            {
                return value;
            }
        }
        return null;
    }
}
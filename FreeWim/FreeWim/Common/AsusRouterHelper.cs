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
            var baseUrl = _configuration.GetValue<string>("AsusRouter:RouterIp", "http://192.168.50.1");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = $"{baseUrl}/update_clients.asp?_={timestamp}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // 设置请求头
            request.Headers.TryAddWithoutValidation("Accept", "text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01");
            request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Referer", $"{baseUrl}/device-map/clients.asp");
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
            var response = new AsusRouterResponse();
            var allDevices = new List<AsusRouterDevice>();

            // 直接提取 fromNetworkmapd 数组
            var fromNetworkmapd = ExtractArray(responseContent, "fromNetworkmapd");
            if (!string.IsNullOrEmpty(fromNetworkmapd))
            {
                var devices = ParseDeviceArray(fromNetworkmapd, "networkmapd");
                allDevices.AddRange(devices);
                response.FromNetworkmapCount = devices.Count;
            }

            // 直接提取 nmpClient 数组
            var nmpClient = ExtractArray(responseContent, "nmpClient");
            if (!string.IsNullOrEmpty(nmpClient))
            {
                var devices = ParseDeviceArray(nmpClient, "nmpClient");
                allDevices.AddRange(devices);
                response.FromNmpClientCount = devices.Count;
            }

            response.Devices = allDevices;

            _logger.LogInformation($"解析完成: 总设备数={allDevices.Count}, networkmapd={response.FromNetworkmapCount}, nmpClient={response.FromNmpClientCount}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"解析响应数据失败: {ex.Message}");

            // 记录原始响应内容的前几行以便调试
            var lines = responseContent.Split('\n');
            var previewLines = string.Join("\n", lines.Take(10));
            _logger.LogError($"响应内容前10行:\n{previewLines}");

            throw new Exception($"解析响应数据失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从响应中提取指定属性的数组内容
    /// </summary>
    private string ExtractArray(string responseContent, string propertyName)
    {
        try
        {
            // 查找属性名开始位置: propertyName : [
            var searchPattern = $"{propertyName} : [";
            var startIndex = responseContent.IndexOf(searchPattern);

            if (startIndex == -1)
            {
                _logger.LogWarning($"未找到属性 {propertyName}");
                return string.Empty;
            }

            // 从 [ 开始
            startIndex = responseContent.IndexOf('[', startIndex);
            if (startIndex == -1) return string.Empty;

            // 查找匹配的 ]
            var bracketCount = 0;
            var endIndex = startIndex;

            for (var i = startIndex; i < responseContent.Length; i++)
                if (responseContent[i] == '[')
                {
                    bracketCount++;
                }
                else if (responseContent[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }

            var arrayContent = responseContent.Substring(startIndex, endIndex - startIndex + 1);
            _logger.LogDebug($"提取的 {propertyName} 数组长度: {arrayContent.Length}");

            return arrayContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"提取数组 {propertyName} 失败");
            return string.Empty;
        }
    }

    /// <summary>
    /// 解析设备数组
    /// </summary>
    private List<AsusRouterDevice> ParseDeviceArray(string arrayJson, string dataSource)
    {
        var devices = new List<AsusRouterDevice>();

        try
        {
            var arrayElement = JsonDocument.Parse(arrayJson).RootElement;

            if (arrayElement.ValueKind != JsonValueKind.Array || arrayElement.GetArrayLength() == 0) return devices;

            var element = arrayElement[0];

            // 获取MAC地址列表
            if (element.TryGetProperty("maclist", out var macList) && macList.ValueKind == JsonValueKind.Array)
                foreach (var mac in macList.EnumerateArray())
                {
                    var macString = mac.GetString();
                    if (!string.IsNullOrEmpty(macString) && element.TryGetProperty(macString, out var deviceElement))
                    {
                        var device = ParseDevice(deviceElement, dataSource);
                        devices.Add(device);
                    }
                }

            // 获取ClientAPILevel（如果有）
            if (element.TryGetProperty("ClientAPILevel", out var apiLevel)) _logger.LogDebug($"{dataSource} ClientAPILevel: {apiLevel.GetString()}");

            return devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"解析 {dataSource} 设备数组失败");
            return devices;
        }
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
    /// 获取字符串属性
    /// </summary>
    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop)) return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();

        return null;
    }

    /// <summary>
    /// 获取整数属性
    /// </summary>
    private static int? GetIntProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                return value;

        return null;
    }

    /// <summary>
    /// 获取设备流量统计数据
    /// </summary>
    /// <param name="mac">设备MAC地址（需要URL编码）</param>
    /// <param name="date">查询日期（Unix时间戳，秒级）</param>
    /// <param name="mode">模式（hour:按小时, day:按天）</param>
    /// <param name="dura">持续时间（小时数，默认24小时）</param>
    /// <returns>24小时的流量数据数组</returns>
    public async Task<List<(long Upload, long Download)>> GetDeviceTrafficAsync(string mac, long date, string mode = "hour", int dura = 24)
    {
        try
        {
            var baseUrl = _configuration.GetValue<string>("AsusRouter:RouterIp", "http://192.168.50.1");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var encodedMac = Uri.EscapeDataString(mac);
            var url = $"{baseUrl}/getWanTraffic.asp?client={encodedMac}&mode={mode}&dura={dura}&date={date}&_={timestamp}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // 设置请求头
            request.Headers.TryAddWithoutValidation("Accept", "text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01");
            request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Referer", $"{baseUrl}/TrafficAnalyzer_Statistic.asp");
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

            // 设置Cookie
            var token = _tokenService.GetAsusRouterTokenAsync();
            var cookie = $"hwaddr=7C:10:C9:E8:6D:C8; apps_last=; bw_rtab=WIRED; maxBandwidth=100; ASUS_TrafficMonitor_unit=1; ASUS_Traffic_unit=2; asus_token={token}; clickedItem_tab=7";
            request.Headers.TryAddWithoutValidation("Cookie", cookie);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"成功获取设备 {mac} 的流量数据，内容长度: {content.Length}");

            return ParseTrafficResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取设备 {mac} 流量数据失败");
            throw new Exception($"获取设备流量数据失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 解析流量响应内容
    /// 响应格式：var array_statistics = [[upload1, download1], [upload2, download2], ...]
    /// </summary>
    private List<(long Upload, long Download)> ParseTrafficResponse(string responseContent)
    {
        var result = new List<(long Upload, long Download)>();

        try
        {
            // 查找 array_statistics = 开始位置
            var startPattern = "array_statistics = [";
            var startIndex = responseContent.IndexOf(startPattern);

            if (startIndex == -1)
            {
                _logger.LogWarning("未找到 array_statistics 数据");
                return result;
            }

            // 从 [ 开始提取数组
            startIndex = responseContent.IndexOf('[', startIndex);
            if (startIndex == -1) return result;

            // 查找匹配的 ]
            var bracketCount = 0;
            var endIndex = startIndex;

            for (var i = startIndex; i < responseContent.Length; i++)
                if (responseContent[i] == '[')
                {
                    bracketCount++;
                }
                else if (responseContent[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }

            var arrayContent = responseContent.Substring(startIndex, endIndex - startIndex + 1);
            _logger.LogDebug($"提取的流量数组: {arrayContent}");

            // 使用 JSON 解析数组
            var arrayElement = JsonDocument.Parse(arrayContent).RootElement;

            if (arrayElement.ValueKind == JsonValueKind.Array)
                foreach (var item in arrayElement.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() == 2)
                    {
                        var upload = item[0].GetInt64();
                        var download = item[1].GetInt64();
                        result.Add((upload, download));
                    }

            _logger.LogInformation($"解析流量数据完成，共 {result.Count} 条记录");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析流量响应数据失败");
            throw new Exception($"解析流量数据失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 保存设备流量统计数据到数据库（批量插入）
    /// </summary>
    /// <param name="mac">设备MAC地址</param>
    /// <param name="statDate">统计日期</param>
    /// <param name="trafficData">24小时流量数据</param>
    public async Task<int> SaveDeviceTrafficToDatabaseAsync(string mac, DateTime statDate, List<(long Upload, long Download)> trafficData)
    {
        using IDbConnection dbConnection = new NpgsqlConnection(_configuration["Connection"]);

        try
        {
            var now = DateTime.Now;

            // 确保只处理24小时数据
            var maxHours = Math.Min(trafficData.Count, 24);

            // 构建批量插入的参数列表
            var batchInsertParams = new List<object>();
            for (var hour = 0; hour < maxHours; hour++)
            {
                var traffic = trafficData[hour];
                batchInsertParams.Add(new
                {
                    Id = Guid.NewGuid().ToString(),
                    Mac = mac,
                    StatDate = statDate.AddDays(-1).Date,
                    Hour = hour,
                    UploadBytes = traffic.Upload,
                    DownloadBytes = traffic.Download,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // 批量插入
            var insertSql = @"
                INSERT INTO asusrouterdevicetraffic (
                    id, mac, statdate, hour, uploadbytes, downloadbytes, createdat, updatedat
                ) VALUES (
                    @Id, @Mac, @StatDate, @Hour, @UploadBytes, @DownloadBytes, @CreatedAt, @UpdatedAt
                )";

            var savedCount = await dbConnection.ExecuteAsync(insertSql, batchInsertParams);

            _logger.LogInformation($"成功批量保存设备 {mac} 在 {statDate:yyyy-MM-dd} 的 {savedCount} 小时流量数据到数据库");
            return savedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"批量保存设备 {mac} 流量数据到数据库失败");
            throw new Exception($"批量保存流量数据到数据库失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取设备流量详细统计数据（按应用/协议分类）
    /// </summary>
    /// <param name="mac">设备MAC地址（需要URL编码）</param>
    /// <param name="date">查询日期（Unix时间戳，秒级）</param>
    /// <param name="mode">模式（detail:详细统计）</param>
    /// <param name="dura">持续时间（小时数，默认24小时）</param>
    /// <returns>按应用/协议分类的流量数据列表</returns>
    public async Task<List<(string AppName, long Upload, long Download)>> GetDeviceTrafficDetailAsync(string mac, long date, string mode = "detail", int dura = 24)
    {
        try
        {
            var baseUrl = _configuration.GetValue<string>("AsusRouter:RouterIp", "http://192.168.50.1");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var encodedMac = Uri.EscapeDataString(mac);
            var url = $"{baseUrl}/getWanTraffic.asp?client={encodedMac}&mode={mode}&dura={dura}&date={date}&_={timestamp}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // 设置请求头
            request.Headers.TryAddWithoutValidation("Accept", "text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01");
            request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Referer", $"{baseUrl}/TrafficAnalyzer_Statistic.asp");
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

            // 设置Cookie
            var token = _tokenService.GetAsusRouterTokenAsync();
            var cookie = $"hwaddr=7C:10:C9:E8:6D:C8; apps_last=; bw_rtab=WIRED; maxBandwidth=100; ASUS_TrafficMonitor_unit=1; ASUS_Traffic_unit=2; asus_token={token}; clickedItem_tab=7";
            request.Headers.TryAddWithoutValidation("Cookie", cookie);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"成功获取设备 {mac} 的详细流量数据，内容长度: {content.Length}");

            return ParseTrafficDetailResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取设备 {mac} 详细流量数据失败");
            throw new Exception($"获取设备详细流量数据失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 解析详细流量响应内容
    /// 响应格式：var array_statistics = [["AppName", upload, download], ...]
    /// </summary>
    private List<(string AppName, long Upload, long Download)> ParseTrafficDetailResponse(string responseContent)
    {
        var result = new List<(string AppName, long Upload, long Download)>();

        try
        {
            // 查找 array_statistics = 开始位置
            var startPattern = "array_statistics = [";
            var startIndex = responseContent.IndexOf(startPattern);

            if (startIndex == -1)
            {
                _logger.LogWarning("未找到 array_statistics 数据");
                return result;
            }

            // 从 [ 开始提取数组
            startIndex = responseContent.IndexOf('[', startIndex);
            if (startIndex == -1) return result;

            // 查找匹配的 ]
            var bracketCount = 0;
            var endIndex = startIndex;

            for (var i = startIndex; i < responseContent.Length; i++)
                if (responseContent[i] == '[')
                {
                    bracketCount++;
                }
                else if (responseContent[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }

            var arrayContent = responseContent.Substring(startIndex, endIndex - startIndex + 1);
            _logger.LogDebug($"提取的详细流量数组: {arrayContent}");

            // 使用 JSON 解析数组
            var arrayElement = JsonDocument.Parse(arrayContent).RootElement;

            if (arrayElement.ValueKind == JsonValueKind.Array)
                foreach (var item in arrayElement.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() == 3)
                    {
                        var appName = item[0].GetString() ?? "Unknown";
                        var upload = item[1].GetInt64();
                        var download = item[2].GetInt64();
                        result.Add((appName, upload, download));
                    }

            _logger.LogInformation($"解析详细流量数据完成，共 {result.Count} 条记录");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析详细流量响应数据失败");
            throw new Exception($"解析详细流量数据失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 保存设备详细流量统计数据到数据库
    /// </summary>
    /// <param name="mac">设备MAC地址</param>
    /// <param name="statDate">统计日期</param>
    /// <param name="trafficDetailData">按应用/协议分类的流量数据</param>
    public async Task<int> SaveDeviceTrafficDetailToDatabaseAsync(string mac, DateTime statDate, List<(string AppName, long Upload, long Download)> trafficDetailData)
    {
        using IDbConnection dbConnection = new NpgsqlConnection(_configuration["Connection"]);

        try
        {
            var now = DateTime.Now;
            var savedCount = 0;

            foreach (var traffic in trafficDetailData)
            {
                // 插入新记录
                var insertSql = @"
                        INSERT INTO asusrouterdevicetrafficdetail (
                            id, mac, statdate, appname, uploadbytes, downloadbytes, createdat, updatedat
                        ) VALUES (
                            @Id, @Mac, @StatDate, @AppName, @UploadBytes, @DownloadBytes, @CreatedAt, @UpdatedAt
                        )";

                await dbConnection.ExecuteAsync(insertSql, new
                {
                    Id = Guid.NewGuid().ToString(),
                    Mac = mac,
                    StatDate = statDate.AddDays(-1).Date,
                    AppName = traffic.AppName,
                    UploadBytes = traffic.Upload,
                    DownloadBytes = traffic.Download,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                savedCount++;
            }

            _logger.LogInformation($"成功保存设备 {mac} 在 {statDate:yyyy-MM-dd} 的 {savedCount} 条详细流量数据到数据库");
            return savedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"保存设备 {mac} 详细流量数据到数据库失败");
            throw new Exception($"保存详细流量数据到数据库失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 路由器设备同步
    /// 每天凌晨1点执行，获取路由器连接设备并保存到数据库
    /// </summary>
    public async Task SyncRouterDevices()
    {
        var devices = await GetNetworkDevicesAsync();
        var savedCount = await SaveDevicesToDatabaseAsync(devices);
    }

    /// <summary>
    /// 设备流量统计
    /// 每天凌晨2点执行，查询前一天每个设备的流量并存入数据库
    /// 同时采集小时级流量和按应用/协议分类的详细流量
    /// </summary>
    public async Task SyncDeviceTraffic()
    {
        try
        {
            // 获取前一天的日期
            var yesterday = DateTime.Now.Date;
            var dateTimestamp = new DateTimeOffset(yesterday).ToUnixTimeSeconds();

            using IDbConnection dbConnection = new NpgsqlConnection(_configuration["Connection"]);

            // 查询所有在线设备的MAC地址
            var devices = await dbConnection.QueryAsync<string>(
                "SELECT DISTINCT mac FROM asusrouterdevice WHERE mac IS NOT NULL AND mac != ''"
            );

            var enumerable = devices.ToList();
            if (enumerable.Count == 0) return;

            foreach (var mac in enumerable)
                try
                {
                    //1. 获取小时级流量数据 (mode=hour)
                    var trafficData = await GetDeviceTrafficAsync(mac, dateTimestamp, "hour", 24);

                    if (trafficData.Count > 0)
                    {
                        // 检查是否有有效流量数据（上传+下载总和大于0）
                        var hasValidData = trafficData.Any(t => t.Upload > 0 || t.Download > 0);
                        if (hasValidData)
                        {
                            var savedCount = await SaveDeviceTrafficToDatabaseAsync(mac, yesterday, trafficData);
                        }
                    }

                    // 稍微延迟，避免请求过于频繁
                    await Task.Delay(500);

                    // 2. 获取详细流量数据 (mode=detail)
                    var trafficDetailData = await GetDeviceTrafficDetailAsync(mac, dateTimestamp, "detail", 24);

                    if (trafficDetailData.Count > 0)
                    {
                        // 检查是否有有效流量数据（上传+下载总和大于0）
                        var hastrafficDetailData = trafficDetailData.Any(t => t.Upload > 0 || t.Download > 0);
                        if (hastrafficDetailData)
                        {
                            var savedDetailCount = await SaveDeviceTrafficDetailToDatabaseAsync(mac, yesterday, trafficDetailData);
                        }
                    }

                    // 防止请求过于频繁，稍微延迟
                    await Task.Delay(1000);
                }
                catch (Exception)
                {
                }
        }
        catch (Exception)
        {
            // ignored
        }
    }
}
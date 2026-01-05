using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dapper;
using FreeWim.Models;
using Npgsql;

namespace FreeWim.Common;

/// <summary>
/// 网络测速服务
/// </summary>
public class SpeedTestService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpeedTestService> _logger;
    private readonly PushMessageHelper _pushMessageHelper;
    private readonly int _maxRetryAttempts;
    private readonly int _retryDelayMilliseconds;

    public SpeedTestService(IConfiguration configuration, ILogger<SpeedTestService> logger, PushMessageHelper pushMessageHelper)
    {
        _configuration = configuration;
        _logger = logger;
        _pushMessageHelper = pushMessageHelper;

        // 从配置读取重试参数，提供默认值
        _maxRetryAttempts = _configuration.GetValue<int>("SpeedTest:MaxRetryAttempts", 10);
        _retryDelayMilliseconds = _configuration.GetValue<int>("SpeedTest:RetryDelayMilliseconds", 5000);
    }

    /// <summary>
    /// 执行网络测速核心逻辑（可被 API 和 Hangfire 定时任务调用）
    /// </summary>
    /// <returns>测速结果响应对象</returns>
    public async Task<SpeedRecordResponse> ExecuteSpeedTestAsync()
    {
        Exception? lastException = null;

        // 重试逻辑：最多尝试 _maxRetryAttempts 次
        for (var attempt = 1; attempt <= _maxRetryAttempts; attempt++)
            try
            {
                _logger.LogInformation($"开始第 {attempt} 次测速尝试（最多 {_maxRetryAttempts} 次）");
                return await ExecuteSpeedTestInternalAsync();
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, $"第 {attempt} 次测速失败: {ex.Message}");

                // 如果还有重试机会，等待后重试
                if (attempt < _maxRetryAttempts)
                {
                    // 指数退避：每次重试等待时间递增
                    var delayMs = _retryDelayMilliseconds * attempt;
                    _logger.LogInformation($"等待 {delayMs}ms 后进行第 {attempt + 1} 次重试...");
                    await Task.Delay(delayMs);
                }
            }

        // 所有重试都失败后，记录失败并抛出异常
        _logger.LogError(lastException, $"测速失败：已重试 {_maxRetryAttempts} 次仍然失败");
        await SaveFailedRecordAsync($"重试 {_maxRetryAttempts} 次后失败: {lastException?.Message}");
        throw new Exception($"测速失败：已重试 {_maxRetryAttempts} 次，最后错误: {lastException?.Message}", lastException);
    }

    /// <summary>
    /// 执行单次测速（内部方法，供重试逻辑调用）
    /// </summary>
    private async Task<SpeedRecordResponse> ExecuteSpeedTestInternalAsync()
    {
        IDbConnection? dbConnection = null;
        try
        {
            // 1. 确定运行的文件路径和名称
            var fileName = GetSpeedTestExecutablePath();

            // 2. 执行测速
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                // 参数说明：
                // --format=json: 输出JSON格式
                // --accept-license & --accept-gdpr: 自动化运行必须跳过交互确认
                Arguments = "--format=json --accept-license --accept-gdpr",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // 读取标准输出
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) throw new Exception($"测速进程返回非零退出码: {error}");

            // 3. 解析 JSON 结果
            using var jsonDoc = JsonDocument.Parse(output);
            var root = jsonDoc.RootElement;

            // 提取数据
            var ping = root.GetProperty("ping").GetProperty("latency").GetDouble();
            var downloadBps = root.GetProperty("download").GetProperty("bandwidth").GetDouble();
            var uploadBps = root.GetProperty("upload").GetProperty("bandwidth").GetDouble();

            var server = root.GetProperty("server");
            var serverId = server.GetProperty("id").GetDouble();
            var serverHost = server.GetProperty("host").GetString();
            var serverName = server.GetProperty("name").GetString();

            var resultUrl = root.GetProperty("result").GetProperty("url").GetString();

            // 4. 转换速度单位：bytes/s -> Mbit/s
            // bandwidth 单位是 bytes per second，转换为 Mbit/s: (bytes/s * 8) / 1,000,000
            var downloadMbps = Math.Round((decimal)(downloadBps * 8 / 1_000_000), 2);
            var uploadMbps = Math.Round((decimal)(uploadBps * 8 / 1_000_000), 2);

            // 5. 保存到数据库
            var recordId = Guid.NewGuid().ToString();
            var speedRecord = new SpeedRecord
            {
                id = recordId,
                ping = $"{ping}",
                download = downloadMbps,
                upload = uploadMbps,
                server_id = serverId,
                server_host = serverHost,
                server_name = serverName,
                url = resultUrl,
                scheduled = 0,
                failed = 0,
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };

            dbConnection = new NpgsqlConnection(_configuration["Connection"]);
            var sql = @"
                INSERT INTO speedrecord (
                    id, ping, download, upload, server_id, server_host, 
                    server_name, url, scheduled, failed, created_at, updated_at
                ) VALUES (
                    @id, @ping, @download, @upload, @server_id, @server_host, 
                    @server_name, @url, @scheduled, @failed, @created_at, @updated_at
                )";

            await dbConnection.ExecuteAsync(sql, speedRecord);
            _logger.LogInformation($"测速记录已保存，ID: {recordId}");

            // 6. 返回友好格式的 JSON
            var response = new SpeedRecordResponse
            {
                Message = "测速成功",
                Data = new SpeedRecordDto
                {
                    Id = recordId,
                    Ping = $"{ping} ms",
                    Download = $"{downloadMbps} Mbit/s",
                    Upload = $"{uploadMbps} Mbit/s",
                    ServerName = serverName,
                    ServerHost = serverHost,
                    Url = resultUrl,
                    TestTime = DateTime.Now
                }
            };

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "单次测速执行异常");
            throw; // 重新抛出异常，让重试逻辑处理
        }
        finally
        {
            dbConnection?.Dispose();
        }
    }

    /// <summary>
    /// 获取最新的测速记录
    /// </summary>
    public SpeedRecordDto? GetLatestRecord()
    {
        try
        {
            using IDbConnection dbConnection = new NpgsqlConnection(_configuration["Connection"]);
            var speedRecord = dbConnection.Query<SpeedRecord>(
                "SELECT * FROM speedrecord WHERE failed = 0 OR failed IS NULL ORDER BY created_at DESC LIMIT 1"
            ).FirstOrDefault();

            if (speedRecord == null) return null;

            return new SpeedRecordDto
            {
                Id = speedRecord.id,
                Ping = speedRecord.ping,
                Download = speedRecord.download.HasValue ? $"{speedRecord.download.Value}" : "N/A",
                Upload = speedRecord.upload.HasValue ? $"{speedRecord.upload.Value}" : "N/A",
                ServerName = speedRecord.server_name,
                ServerHost = speedRecord.server_host,
                Url = speedRecord.url,
                TestTime = speedRecord.created_at
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询测速记录异常");
            throw;
        }
    }

    /// <summary>
    /// 根据日期范围获取测速记录
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <returns>测速记录列表</returns>
    public List<SpeedRecordForChart> GetRecordsByDateRange(DateTime startDate, DateTime endDate)
    {
        try
        {
            using IDbConnection dbConnection = new NpgsqlConnection(_configuration["Connection"]);

            var sql = @"
                SELECT 
                    id,
                    created_at,
                    download,
                    upload,
                    CAST(ping AS DECIMAL) as ping,
                    server_name
                FROM speedrecord 
                WHERE (failed = 0 OR failed IS NULL)
                  AND created_at >= @StartDate 
                  AND created_at <= @EndDate
                ORDER BY created_at DESC";

            var records = dbConnection.Query<SpeedRecordForChart>(sql, new
            {
                StartDate = startDate,
                EndDate = endDate.AddDays(1).AddSeconds(-1) // 包含结束日期的全天
            }).ToList();

            _logger.LogInformation($"查询到 {records.Count} 条测速记录，时间范围：{startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd}");

            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据日期范围查询测速记录异常");
            throw;
        }
    }

    /// <summary>
    /// 获取 speedtest 可执行文件路径
    /// </summary>
    private string GetSpeedTestExecutablePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows 环境：从项目 wwwroot/tools 目录获取
            var baseDirectory = AppContext.BaseDirectory;
            var toolsPath = Path.Combine(baseDirectory, "wwwroot", "tools");
            var fileName = Path.Combine(toolsPath, "speedtest.exe");

            // 检查文件是否存在
            if (!File.Exists(fileName)) throw new FileNotFoundException($"测速工具不存在: {fileName}");

            return fileName;
        }
        else
        {
            // Linux/Docker 环境：使用系统全局安装的 speedtest（通过 apt 安装）
            var fileName = "speedtest";

            // 可选：检查命令是否可用
            try
            {
                var checkProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = "speedtest",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                checkProcess.Start();
                checkProcess.WaitForExit();

                if (checkProcess.ExitCode != 0) throw new FileNotFoundException("测速工具未安装。请在 Docker 镜像中安装 speedtest-cli");
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                // 如果 which 命令不可用，继续尝试运行
                _logger.LogWarning("无法验证 speedtest 命令是否存在: {Message}", ex.Message);
            }

            return fileName;
        }
    }

    /// <summary>
    /// 保存失败记录
    /// </summary>
    private async Task SaveFailedRecordAsync(string errorMessage)
    {
        try
        {
            using var dbConnection = new NpgsqlConnection(_configuration["Connection"]);
            var sql = @"
                INSERT INTO speedrecord (
                    id, ping, failed, created_at, updated_at
                ) VALUES (
                    @id, @ping, @failed, @created_at, @updated_at
                )";

            await dbConnection.ExecuteAsync(sql, new
            {
                id = Guid.NewGuid().ToString(),
                ping = errorMessage?.Substring(0, Math.Min(errorMessage.Length, 200)),
                failed = 1,
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存失败记录时出错");
        }
    }

    /// <summary>
    /// 网速异常提醒
    /// 每天早上10点执行，查询最后一条测速记录，如果上传速度低于配置中的默认值则进行推送提醒
    /// 如果没有配置，就默认上传低于10 Mbit/s提醒
    /// </summary>
    public void CheckSpeedAbnormal()
    {
        try
        {
            // 获取配置的上传速度阈值，默认为10 Mbit/s
            var uploadThreshold = decimal.Parse(_configuration["SpeedTest:UploadThreshold"] ?? "10");
            // 获取最后一条测速记录
            var latestRecord = GetLatestRecord();
            if (latestRecord == null) return;

            // 解析上传速度（移除单位 Mbit/s）
            var uploadSpeedStr = latestRecord.Upload?.Replace(" Mbit/s", "").Trim();
            if (string.IsNullOrEmpty(uploadSpeedStr) || uploadSpeedStr == "N/A") return;

            if (!decimal.TryParse(uploadSpeedStr, out var uploadSpeed)) return;

            // 判断是否低于阈值
            if (uploadSpeed < uploadThreshold)
            {
                var message = $"网络异常预警\n" +
                              $"测试时间: {latestRecord.TestTime:yyyy-MM-dd HH:mm:ss}\n" +
                              $"上传速度: {latestRecord.Upload}Mbit/s\n" +
                              $"下载速度: {latestRecord.Download}Mbit/s";
                _pushMessageHelper.Push("网速异常提醒", message, PushMessageHelper.PushIcon.SpeedTest);
            }
        }
        catch (Exception ex)
        {
            _pushMessageHelper.Push("网速异常提醒任务异常", ex.Message, PushMessageHelper.PushIcon.Alert);
        }
    }
}
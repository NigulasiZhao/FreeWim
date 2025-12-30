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

    public SpeedTestService(IConfiguration configuration, ILogger<SpeedTestService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 执行网络测速核心逻辑（可被 API 和 Hangfire 定时任务调用）
    /// </summary>
    /// <returns>测速结果响应对象</returns>
    public async Task<SpeedRecordResponse> ExecuteSpeedTestAsync()
    {
        IDbConnection? dbConnection = null;
        try
        {
            // 1. 确定运行的文件路径和名称
            string fileName = GetSpeedTestExecutablePath();

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
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                // 记录失败记录
                await SaveFailedRecordAsync(error);
                throw new Exception($"测速失败: {error}");
            }

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
            decimal downloadMbps = Math.Round((decimal)(downloadBps * 8 / 1_000_000), 2);
            decimal uploadMbps = Math.Round((decimal)(uploadBps * 8 / 1_000_000), 2);

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
            _logger.LogError(ex, "测速异常");
            await SaveFailedRecordAsync(ex.Message);
            throw; // 重新抛出异常，让调用方处理
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
            
            if (speedRecord == null)
            {
                return null;
            }

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
    /// 获取 speedtest 可执行文件路径
    /// </summary>
    private string GetSpeedTestExecutablePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows 环境：从项目 wwwroot/tools 目录获取
            string baseDirectory = AppContext.BaseDirectory;
            string toolsPath = Path.Combine(baseDirectory, "wwwroot", "tools");
            string fileName = Path.Combine(toolsPath, "speedtest.exe");
            
            // 检查文件是否存在
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"测速工具不存在: {fileName}");
            }
            
            return fileName;
        }
        else
        {
            // Linux/Docker 环境：使用系统全局安装的 speedtest（通过 apt 安装）
            string fileName = "speedtest";
            
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
                
                if (checkProcess.ExitCode != 0)
                {
                    throw new FileNotFoundException("测速工具未安装。请在 Docker 镜像中安装 speedtest-cli");
                }
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
}

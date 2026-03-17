namespace FreeWim.Mcp;

using System.Text.Json;
using System.Text.Json.Serialization;
using FreeWim.Services;
using Newtonsoft.Json.Linq;

/// <summary>
/// MCP服务器 - 用于HTTP API调用
/// </summary>
public class McpServer
{
    private readonly McpService _mcpService;
    private readonly ILogger<McpServer> _logger;

    public McpServer(McpService mcpService, ILogger<McpServer> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }

    /// <summary>
    /// 获取MCP服务器信息
    /// </summary>
    public McpServerInfo GetServerInfo()
    {
        return new McpServerInfo
        {
            Name = "FreeWim MCP Server",
            Version = "1.0.0",
            Description = "FreeWim工作流自动化系统的MCP服务器，提供PMIS、禅道、考勤等功能接口"
        };
    }

    /// <summary>
    /// 获取所有可用工具（MCP格式）
    /// </summary>
    public List<object> GetTools()
    {
        var tools = _mcpService.GetAvailableTools();
        return tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema
        }).Cast<object>().ToList();
    }

    /// <summary>
    /// 处理工具调用请求
    /// </summary>
    public async Task<McpToolResult> CallTool(string toolName, Dictionary<string, object>? arguments = null)
    {
        try
        {
            Newtonsoft.Json.Linq.JObject? input = null;
            if (arguments != null)
            {
                var json = JsonSerializer.Serialize(arguments);
                input = Newtonsoft.Json.Linq.JObject.Parse(json);
            }

            var result = await _mcpService.ExecuteToolAsync(toolName, input);

            return new McpToolResult
            {
                Success = true,
                Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calling tool '{toolName}': {ex.Message}");
            return new McpToolResult
            {
                Success = false,
                Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Error: {ex.Message}"
                    }
                }
            };
        }
    }
}

/// <summary>
/// MCP服务器信息
/// </summary>
public class McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// MCP工具定义
/// </summary>
public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public McpToolSchema InputSchema { get; set; } = new();
}

/// <summary>
/// MCP工具Schema
/// </summary>
public class McpToolSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

/// <summary>
/// MCP工具执行结果
/// </summary>
public class McpToolResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = new();
}

/// <summary>
/// MCP内容
/// </summary>
public class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

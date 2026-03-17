using FreeWim.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/mcp")]
[Tags("MCP")]
public class McpController(McpService mcpService, ILogger<McpController> logger) : Controller
{
    /// <summary>
    /// MCP根端点：返回服务器信息和工具列表
    /// </summary>
    [HttpGet]
    [Route("")]
    [EndpointSummary("获取MCP服务器信息和工具列表")]
    public IActionResult GetServerInfo()
    {
        try
        {
            var tools = mcpService.GetAvailableTools();
            var toolList = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = t.InputSchema
            }).ToList();

            return Ok(new
            {
                server = new
                {
                    name = "FreeWim MCP Server",
                    version = "1.0.0",
                    description = "FreeWim工作流自动化系统的MCP服务器，提供PMIS、禅道、考勤等功能接口"
                },
                tools = toolList
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting server info: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// MCP协议：获取工具列表
    /// </summary>
    [HttpPost]
    [Route("tools/list")]
    [EndpointSummary("获取MCP工具列表")]
    public IActionResult ListTools()
    {
        try
        {
            var tools = mcpService.GetAvailableTools();
            var toolList = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = t.InputSchema
            }).ToList();

            return Ok(new
            {
                tools = toolList
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error listing MCP tools: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// MCP协议：调用工具
    /// </summary>
    [HttpPost]
    [Route("tools/call")]
    [EndpointSummary("调用MCP工具")]
    public async Task<IActionResult> CallTool([FromBody] McpToolCallRequest request)
    {
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest(new { error = "Tool name is required" });

        try
        {
            var result = await mcpService.ExecuteToolAsync(request.Name, request.Arguments);
            return Ok(new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = System.Text.Json.JsonSerializer.Serialize(result)
                    }
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning($"Tool not found: {request.Name}");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error executing MCP tool '{request.Name}': {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定工具的详细信息（自定义端点）
    /// </summary>
    [HttpGet]
    [Route("tools/{toolName}")]
    [EndpointSummary("获取工具详情")]
    public IActionResult GetToolInfo(string toolName)
    {
        try
        {
            var tools = mcpService.GetAvailableTools();
            var tool = tools.FirstOrDefault(t => t.Name == toolName);

            if (tool == null)
                return NotFound(new { error = $"Tool '{toolName}' not found" });

            return Ok(new
            {
                name = tool.Name,
                description = tool.Description,
                inputSchema = tool.InputSchema
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting tool info: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}

/// <summary>
/// MCP工具调用请求
/// </summary>
public class McpToolCallRequest
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工具输入参数
    /// </summary>
    public JObject? Arguments { get; set; }
}

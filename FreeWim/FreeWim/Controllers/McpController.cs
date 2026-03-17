using FreeWim.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
[Tags("MCP")]
public class McpController(McpService mcpService, ILogger<McpController> logger) : Controller
{
    /// <summary>
    /// 获取所有可用的MCP工具列表
    /// </summary>
    [HttpGet]
    [EndpointSummary("获取MCP工具列表")]
    public IActionResult ListTools()
    {
        try
        {
            var tools = mcpService.GetAvailableTools();
            var toolList = tools.Select(t => new
            {
                t.Name,
                t.Description,
                t.Category,
                t.InputSchema
            }).ToList();

            return Ok(new
            {
                success = true,
                data = toolList,
                count = toolList.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error listing MCP tools: {ex.Message}");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 调用MCP工具
    /// </summary>
    [HttpPost]
    [EndpointSummary("调用MCP工具")]
    public async Task<IActionResult> CallTool([FromBody] McpToolRequest request)
    {
        if (string.IsNullOrEmpty(request.ToolName))
            return BadRequest(new { success = false, error = "ToolName is required" });

        try
        {
            var result = await mcpService.ExecuteToolAsync(request.ToolName, request.Input);
            return Ok(new
            {
                success = true,
                data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning($"Tool not found: {request.ToolName}");
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error executing MCP tool '{request.ToolName}': {ex.Message}");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定工具的详细信息
    /// </summary>
    [HttpGet]
    [EndpointSummary("获取工具详情")]
    public IActionResult GetToolInfo(string toolName)
    {
        try
        {
            var tools = mcpService.GetAvailableTools();
            var tool = tools.FirstOrDefault(t => t.Name == toolName);

            if (tool == null)
                return NotFound(new { success = false, error = $"Tool '{toolName}' not found" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    tool.Name,
                    tool.Description,
                    tool.Category,
                    tool.InputSchema
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting tool info: {ex.Message}");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}

/// <summary>
/// MCP工具调用请求
/// </summary>
public class McpToolRequest
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// 工具输入参数
    /// </summary>
    public JObject? Input { get; set; }
}

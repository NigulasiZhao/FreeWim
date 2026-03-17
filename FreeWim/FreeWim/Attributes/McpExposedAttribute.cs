namespace FreeWim.Attributes;

/// <summary>
/// 标记方法对MCP开放
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class McpExposedAttribute : Attribute
{
    /// <summary>
    /// MCP工具名称
    /// </summary>
    public string ToolName { get; set; }

    /// <summary>
    /// 工具描述
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 工具分类
    /// </summary>
    public string Category { get; set; }

    public McpExposedAttribute(string toolName, string description, string category = "default")
    {
        ToolName = toolName;
        Description = description;
        Category = category;
    }
}

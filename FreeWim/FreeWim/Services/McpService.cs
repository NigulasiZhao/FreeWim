namespace FreeWim.Services;

using System.Reflection;
using FreeWim.Attributes;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

public class McpService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpService> _logger;
    private List<McpToolInfo>? _cachedTools;

    public McpService(IServiceProvider serviceProvider, ILogger<McpService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有MCP工具信息
    /// </summary>
    public List<McpToolInfo> GetAvailableTools()
    {
        if (_cachedTools != null)
            return _cachedTools;

        _cachedTools = new List<McpToolInfo>();
        var assembly = typeof(Program).Assembly;

        // 扫描所有Controller
        var controllerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"))
            .ToList();

        foreach (var controllerType in controllerTypes)
        {
            var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                var mcpAttr = method.GetCustomAttribute<McpExposedAttribute>();
                if (mcpAttr == null)
                    continue;

                var toolInfo = new McpToolInfo
                {
                    Name = mcpAttr.ToolName,
                    Description = mcpAttr.Description,
                    Category = mcpAttr.Category,
                    ControllerType = controllerType,
                    MethodInfo = method,
                    InputSchema = GenerateInputSchema(method)
                };

                _cachedTools.Add(toolInfo);
                _logger.LogInformation($"Registered MCP tool: {mcpAttr.ToolName}");
            }
        }

        return _cachedTools;
    }

    /// <summary>
    /// 执行MCP工具
    /// </summary>
    public async Task<object?> ExecuteToolAsync(string toolName, JObject? input = null)
    {
        var tools = GetAvailableTools();
        var tool = tools.FirstOrDefault(t => t.Name == toolName);

        if (tool == null)
        {
            _logger.LogWarning($"MCP tool not found: {toolName}");
            throw new InvalidOperationException($"Tool '{toolName}' not found");
        }

        try
        {
            // 创建Controller实例
            var controller = ActivatorUtilities.CreateInstance(_serviceProvider, tool.ControllerType);

            // 准备方法参数
            var parameters = tool.MethodInfo.GetParameters();
            var methodArgs = new object?[parameters.Length];

            if (input != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var value = input[param.Name!];

                    if (value != null)
                    {
                        methodArgs[i] = value.ToObject(param.ParameterType);
                    }
                    else if (param.HasDefaultValue)
                    {
                        methodArgs[i] = param.DefaultValue;
                    }
                }
            }

            // 调用方法
            var result = tool.MethodInfo.Invoke(controller, methodArgs);

            // 处理异步方法
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                var resultProperty = task.GetType().GetProperty("Result");
                result = resultProperty?.GetValue(task);
            }

            _logger.LogInformation($"MCP tool executed successfully: {toolName}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error executing MCP tool '{toolName}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 生成方法的输入Schema
    /// </summary>
    private Dictionary<string, object> GenerateInputSchema(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            var paramType = param.ParameterType;
            var jsonType = GetJsonType(paramType);

            // 如果是复杂对象类型，展开其属性
            if (jsonType == "object" && !paramType.IsPrimitive && paramType.Namespace?.StartsWith("FreeWim") == true)
            {
                var objSchema = GenerateObjectSchema(paramType);
                properties[param.Name!] = objSchema;
            }
            else
            {
                var paramSchema = new Dictionary<string, object>
                {
                    ["type"] = jsonType,
                    ["description"] = GetParameterDescription(param)
                };
                properties[param.Name!] = paramSchema;
            }

            if (!param.HasDefaultValue)
                required.Add(param.Name!);
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    /// <summary>
    /// 生成复杂对象的Schema
    /// </summary>
    private Dictionary<string, object> GenerateObjectSchema(Type type)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        var publicProperties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in publicProperties)
        {
            var propType = prop.PropertyType;
            var jsonType = GetJsonType(propType);

            var propSchema = new Dictionary<string, object>
            {
                ["type"] = jsonType,
                ["description"] = GetPropertyDescription(prop)
            };

            properties[prop.Name] = propSchema;

            // 检查属性是否有默认值或是否可为null
            var hasDefaultValue = prop.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>() != null;
            var isNullable = prop.PropertyType.IsGenericType &&
                           prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);

            if (!hasDefaultValue && !isNullable && prop.PropertyType != typeof(string))
                required.Add(prop.Name);
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    /// <summary>
    /// 获取属性描述（从Description特性）
    /// </summary>
    private string GetPropertyDescription(System.Reflection.PropertyInfo prop)
    {
        var descAttr = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        return descAttr?.Description ?? prop.Name ?? "Property";
    }

    /// <summary>
    /// 获取参数的JSON类型
    /// </summary>
    private string GetJsonType(Type type)
    {
        return type.Name switch
        {
            "String" => "string",
            "Int32" or "Int64" => "integer",
            "Double" or "Decimal" => "number",
            "Boolean" => "boolean",
            "DateTime" => "string",
            _ => "object"
        };
    }

    /// <summary>
    /// 获取参数描述（从Description特性）
    /// </summary>
    private string GetParameterDescription(ParameterInfo param)
    {
        var descAttr = param.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        return descAttr?.Description ?? param.Name ?? "Parameter";
    }
}

/// <summary>
/// MCP工具信息
/// </summary>
public class McpToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Type ControllerType { get; set; } = null!;
    public MethodInfo MethodInfo { get; set; } = null!;
    public Dictionary<string, object> InputSchema { get; set; } = null!;
}

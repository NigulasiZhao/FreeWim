using System.ClientModel;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Localization;
using FreeWim;
using System.Globalization;
using Microsoft.Extensions.AI;
using OpenAI;
using Scalar.AspNetCore;
using Serilog;
using FreeWim.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "Logs")) Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "Logs");
Log.Logger = new LoggerConfiguration().WriteTo.Console()
    .WriteTo.File(AppDomain.CurrentDomain.BaseDirectory + "Logs/log.txt", rollingInterval: RollingInterval.Day) // 每天一个文件
    .CreateLogger();

builder.Host.UseSerilog();
// Add services to the container.
var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
var environment = builder.Environment.EnvironmentName;

// 根据环境加载配置文件
builder.Configuration
    .AddJsonFile(Path.Combine(baseDirectory, "appsettings.json"), optional: false, reloadOnChange: true)
    .AddJsonFile(Path.Combine(baseDirectory, $"appsettings.{environment}.json"), optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(); // 添加环境变量支持
builder.Services.AddDirectoryBrowser();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "FreeWim API";
        // 使用 Markdown 插入图片
        document.Info.Description = @"
<div align='center'>
  <img src='/homelogo.png' width='200' />
  <br/>
  <strong>FreeWim 就像一把沉默却锋利的“自动化之刃”，<br/>
    帮助开发者斩断了束缚灵魂的流程锁链。</strong>
    <br/><br/>
    它让每一位开发者明白：<br/>
    代码是我们的梦想，但不应成为我们的枷锁。
    <br/><br/>
    它守护的不仅仅是数据和进度，<br/>
    更是每一位开发者作为“人”的最后一份尊严与自由。
</div>";
        return Task.CompletedTask;
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin() // 允许来自任何源的请求
            .AllowAnyMethod() // 允许任何 HTTP 方法（GET、POST、PUT、DELETE 等）
            .AllowAnyHeader(); // 允许任何请求头
    });
});
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var openAIClient = new OpenAIClient(
        new ApiKeyCredential(cfg["LLM:ApiKey"] ?? string.Empty),
        new OpenAIClientOptions { Endpoint = new Uri(cfg["LLM:EndPoint"] ?? string.Empty) }
    );
    return new ChatClientBuilder(openAIClient.GetChatClient(cfg["LLM:ModelId"]).AsIChatClient())
        .UseFunctionInvocation()
        .Build();
});
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(c =>
            c.UseNpgsqlConnection(builder.Configuration["Connection"]?.ToString()),
        new PostgreSqlStorageOptions
        {
            // 控制过期任务清理频率（默认是1小时，这里改为1分钟）
            JobExpirationCheckInterval = TimeSpan.FromMinutes(5),
            // 批量删除个数
            DeleteExpiredBatchSize = 1000
        }));
builder.Services.AddHangfireServer();
// 自动注册所有 Service 类（包括 Services 目录下的和根目录下的）
var assembly = typeof(Program).Assembly;
var serviceTypes = assembly.GetTypes()
    .Where(t => t.Namespace != null &&
                (t.Namespace == "FreeWim.Services" || t.Namespace == "FreeWim") &&
                t.Name.EndsWith("Service") &&
                t.IsClass &&
                !t.IsAbstract)
    .ToList();

foreach (var serviceType in serviceTypes)
{
    builder.Services.AddSingleton(serviceType);
}

var app = builder.Build();
var zh = new CultureInfo("zh-CN");
zh.DateTimeFormat.FullDateTimePattern = "yyyy-MM-dd HH:mm:ss";
zh.DateTimeFormat.LongDatePattern = "yyyy-MM-dd";
zh.DateTimeFormat.LongTimePattern = "HH:mm:ss";
zh.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
zh.DateTimeFormat.ShortTimePattern = "HH:mm:ss";
IList<CultureInfo> supportedCultures = new List<CultureInfo>
{
    zh
};
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("zh-CN"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializerService>();
    initializer.Initialize();
}

app.UseCors("AllowAll");
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
app.MapOpenApi();
app.MapScalarApiReference();
//}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<Hangfire.Dashboard.IDashboardAuthorizationFilter>()
});
app.Services.GetRequiredService<HangFireService>().StartHangFireTask();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.UseDefaultFiles(); // 支持默认文档，比如 index.html
app.UseStaticFiles(); // 启用 wwwroot 文件夹
app.MapGet("/dashboard", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "AttendanceDashBoard.html"));
});
app.MapGet("/daydashboard", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "DayReportDashBoard.html"));
});
app.MapGet("/daka", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "AttendanceApplication.html"));
});
app.MapGet("/weekovertiem", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "WeekOverTime.html"));
});
app.MapGet("/workhours", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "WorkHours.html"));
});
app.MapGet("/yinuo", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "Yinuo.html"));
});
app.MapGet("/network", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "NetworkDashboard.html"));
});
app.MapGet("/trafficstatistics", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "TrafficMonitoring.html"));
});
app.MapGet("/bnormalattendance", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Path.Combine(AppContext.BaseDirectory, "wwwroot", "views"), "AttendanceAbnormal.html"));
});
app.MapGet("/", () => Results.Redirect("/scalar"));
app.Run();
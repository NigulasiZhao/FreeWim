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
using FreeWim.Common;

var builder = WebApplication.CreateBuilder(args);
if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "Logs")) Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "Logs");
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(AppDomain.CurrentDomain.BaseDirectory + "Logs/log.txt", rollingInterval: RollingInterval.Day) // 每天一个文件
    .CreateLogger();

builder.Host.UseSerilog();
// Add services to the container.
builder.Configuration.AddJsonFile(AppDomain.CurrentDomain.BaseDirectory + "appsettings.json", false, true);
builder.Services.AddDirectoryBrowser();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
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
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<HangFireHelper>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<ZentaoHelper>();
builder.Services.AddSingleton<AttendanceHelper>();
builder.Services.AddSingleton<PmisHelper>();
builder.Services.AddSingleton<PushMessageHelper>();
builder.Services.AddSingleton<WorkFlowExecutor>();
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
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
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
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
});
app.Services.GetRequiredService<HangFireHelper>().StartHangFireTask();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.UseDefaultFiles(); // 支持默认文档，比如 index.html
app.UseStaticFiles(); // 启用 wwwroot 文件夹
app.MapGet("/dashboard", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/AttendanceDashBoard.html");
});
app.MapGet("/daydashboard", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/DayReportDashBoard.html");
});
app.MapGet("/daka", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/AttendanceApplication.html");
});
app.MapGet("/weekovertiem", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/WeekOverTime.html");
});
app.MapGet("/workhours", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/WorkHours.html");
});
app.Run();
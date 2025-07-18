using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using FreeWim.Models;
using FreeWim.SpeedTest;
using System.Data;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using FreeWim.Models.Attendance;
using static FreeWim.SpeedTestHelper;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class SpeedTestController : Controller
{
    private readonly IConfiguration _Configuration;
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    private readonly ILogger<SpeedTestController> _logger;

    public SpeedTestController(IConfiguration configuration, ILogger<SpeedTestController> logger)
    {
        _Configuration = configuration;
        _logger = logger;
    }

    [Tags("网络测速")]
    [EndpointSummary("执行网络测速")]
    [HttpGet]
    public string Index()
    {
        var speedTestHelper = new SpeedTestHelper();
        var speedResult = speedTestHelper.StartSpeedTest();
        return string.Format("下载速度: {0} Mbps;  上传速度: {1} Mbps", speedResult.downloadSpeed, speedResult.uploadSpeed);
        ;
    }

    [Tags("网络测速")]
    [EndpointSummary("网络测速结果查询")]
    [HttpGet]
    public ActionResult latest()
    {
        IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
        var speedRecord = _DbConnection.Query<SpeedRecord>("select * from speedrecord order by created_at desc").First();
        _DbConnection.Dispose();
        var speedRecordResponse = new SpeedRecordResponse();
        speedRecordResponse.Message = "ok";
        speedRecordResponse.Data = speedRecord;
        // 返回结果
        return Json(speedRecordResponse);
    }
}
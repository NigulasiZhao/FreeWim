using Microsoft.AspNetCore.Mvc;
using FreeWim.Models;
using FreeWim.Services;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class SpeedTestController : Controller
{
    private readonly SpeedTestService _speedTestService;
    private readonly ILogger<SpeedTestController> _logger;

    public SpeedTestController(SpeedTestService speedTestService, ILogger<SpeedTestController> logger)
    {
        _speedTestService = speedTestService;
        _logger = logger;
    }

    [Tags("网络测速")]
    [EndpointSummary("执行网络测速")]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var result = await _speedTestService.ExecuteSpeedTestAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测速异常");
            return Problem($"系统异常: {ex.Message}");
        }
    }

    [Tags("网络测速")]
    [EndpointSummary("网络测速结果查询")]
    [HttpGet]
    public ActionResult Latest()
    {
        try
        {
            var data = _speedTestService.GetLatestRecord();
            
            if (data == null)
            {
                return NotFound(new { Message = "没有找到测速记录" });
            }

            var response = new SpeedRecordResponse
            {
                Message = "ok",
                Data = data
            };

            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询测速记录异常");
            return Problem($"查询失败: {ex.Message}");
        }
    }

    [Tags("网络测速")]
    [EndpointSummary("根据日期范围获取测速记录")]
    [HttpGet]
    public ActionResult GetRecordsByDateRange([FromQuery] string? startDate, [FromQuery] string? endDate)
    {
        try
        {
            // 默认获取近30天的数据
            DateTime start = DateTime.Today.AddDays(-29);
            DateTime end = DateTime.Today;

            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var parsedStart))
            {
                start = parsedStart.Date;
            }

            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var parsedEnd))
            {
                end = parsedEnd.Date;
            }

            var records = _speedTestService.GetRecordsByDateRange(start, end);

            return Json(new
            {
                Message = "ok",
                Data = records
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据日期范围查询测速记录异常");
            return Problem($"查询失败: {ex.Message}");
        }
    }
}
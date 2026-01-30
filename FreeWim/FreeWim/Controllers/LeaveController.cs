using FreeWim.Services;
using Microsoft.AspNetCore.Mvc;
using FreeWim.Models.PmisAndZentao.Dto;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class LeaveController(PmisService pmisService) : Controller
{
    [HttpPost]
    public async Task<IActionResult> QueryLeaveApply([FromBody] LeaveQueryRequest query)
    {
        try
        {
            var result = await pmisService.QueryLeaveApply(query);
            return Content(result.ToString(), "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Code = 500, ex.Message });
        }
    }
}

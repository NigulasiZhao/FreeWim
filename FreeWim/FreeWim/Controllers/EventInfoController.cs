using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using FreeWim.Models.EventInfo;
using System.Data;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class EventInfoController : Controller
{
    private readonly IConfiguration _Configuration;

    public EventInfoController(IConfiguration configuration)
    {
        _Configuration = configuration;
    }

    [Tags("事件")]
    [EndpointSummary("事件上报")]
    [HttpPost]
    public ActionResult EventPush([FromBody] EventPushInput input)
    {
        //purple
        using IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
        try
        {
            var Existence = _DbConnection.Query<int>("SELECT COUNT(0) FROM public.eventinfo WHERE Source = :source AND DistinguishingMark = :distinguishingmark",
                new { source = input.Source, distinguishingmark = input.DistinguishingMark }).First();
            if (Existence == 0)
                _DbConnection.Execute(
                    $@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                    , new
                    {
                        id = Guid.NewGuid().ToString(),
                        title = input.Title,
                        message = input.Message,
                        clockintime = string.IsNullOrEmpty(input.Clockintime) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : DateTime.Parse(input.Clockintime).ToString("yyyy-MM-dd HH:mm:ss"),
                        color = input.Color,
                        source = input.Source,
                        distinguishingmark = input.DistinguishingMark
                    });
            _DbConnection.Dispose();
        }
        catch (IOException e)
        {
            _DbConnection.Dispose();
            return Json(e.Message);
        }

        return Json("成功");
    }
}
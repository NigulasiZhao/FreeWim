using System.Data;
using System.Globalization;
using Dapper;
using Npgsql;
using FreeWim.Models.PmisAndZentao;
using FreeWim.Utils;

namespace FreeWim.Services;

/// <summary>
/// 自动消息发送服务
/// </summary>
public class MessageService(IConfiguration configuration, TokenService tokenService)
{
    /// <summary>
    /// 自动发送消息
    /// 在工作日的指定时间段内随机延迟后发送消息
    /// </summary>
    public async Task AutomaticallySendMessage()
    {
        var random = new Random();
        var delayMilliseconds = random.Next(1, 8) * 60 * 1000;
        await Task.Delay(delayMilliseconds);
        await SendMessage();
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    private async Task SendMessage()
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var lastDay = dbConnection.Query<string>($@"select
                                                                                                	checkinrule
                                                                                                from
                                                                                                	public.attendancerecordday
                                                                                                where
                                                                                                	to_char(attendancedate,
                                                                                                	'yyyy-MM-dd') = '{DateTime.Now:yyyy-MM-dd}'").FirstOrDefault();
        if (lastDay is null or "休息") return;
        if (DateTime.Now.Hour < 8 || DateTime.Now.Hour >= 21) return;
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        var httpHelper = new HttpRequestHelper();
        var message = AesHelper.EncryptAes(DateTime.Now.ToString(CultureInfo.InvariantCulture));
        _ = await httpHelper.PostAsync(pmisInfo.Url + $"/uniwim/message/chat/send", new
        {
            content = message,
            receiverName = "",
            receiverPhone = "",
            receiverUserHead = "",
            receiverUserId = "",
            sendName = "",
            sendPhone = "",
            avatar = "",
            sendUserId = "",
            tenantId = "5d89917712441d7a5073058c",
            sendType = 1,
            appType = 1,
            aiType = "deepseek",
            msgId = "3631D4A3115C4463B8C4CE6B1639B5A3"
        },
            new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() ?? string.Empty } });
    }
}

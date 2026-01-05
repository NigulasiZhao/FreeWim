using Newtonsoft.Json.Linq;

namespace FreeWim.Common;

/// <summary>
/// DeepSeek余额监控服务
/// </summary>
public class DeepSeekMonitorService(IConfiguration configuration, PushMessageHelper pushMessageHelper)
{
    /// <summary>
    /// DeepSeek余额预警
    /// 调用deepseek余额查询接口，如可用余额低于1元则进行推送通知
    /// </summary>
    public void CheckBalance()
    {
        var httpRequestHelper = new HttpRequestHelper();
        var postResponse = httpRequestHelper.GetAsync(configuration["LLM:EndPoint"] + "/user/balance",
            new Dictionary<string, string> { { "Authorization", "Bearer " + configuration["LLM:ApiKey"] } }).Result;
        if (postResponse.IsSuccessStatusCode)
        {
            decimal total_balance = 0;
            var pushMessage = "";
            var json = JObject.Parse(postResponse.Content.ReadAsStringAsync().Result);
            if (bool.Parse(json["is_available"]?.ToString() ?? string.Empty))
                pushMessage += "尚有余额可供使用";
            else
                pushMessage += "已无余额可供使用";

            if (!string.IsNullOrEmpty(json["balance_infos"]?.ToString()))
                if (json["balance_infos"] is JArray dataArray)
                    foreach (var jToken in dataArray)
                    {
                        pushMessage += $"\n可用余额: " + jToken["total_balance"] + " " + jToken["currency"];
                        total_balance += decimal.Parse(jToken["total_balance"]?.ToString() ?? string.Empty);
                    }

            if (!bool.Parse(json["is_available"]?.ToString() ?? string.Empty) || total_balance <= 1) 
                pushMessageHelper.Push("余额提醒", pushMessage, PushMessageHelper.PushIcon.DeepSeek);
        }
    }
}

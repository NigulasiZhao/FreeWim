using System.Data;
using Dapper;
using Npgsql;
using FreeWim.Models.EventInfo;
using Newtonsoft.Json;

namespace FreeWim.Common;

/// <summary>
/// Keep运动数据同步服务
/// </summary>
public class KeepDataSyncService(IConfiguration configuration)
{
    /// <summary>
    /// Keep数据同步
    /// 从Keep API获取运动数据并同步到数据库
    /// </summary>
    public void SyncKeepData()
    {
        if (string.IsNullOrEmpty(configuration["KEEPx-bundleId"])
            || string.IsNullOrEmpty(configuration["KEEPx-session-id"])
            || string.IsNullOrEmpty(configuration["KEEPCookie"])
            || string.IsNullOrEmpty(configuration["KEEPx-user-id"])
            || string.IsNullOrEmpty(configuration["KEEPx-keep-timezone"])
            || string.IsNullOrEmpty(configuration["KEEPAuthorization"]))
            return;

        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-bundleId", configuration["KEEPx-bundleId"]);
        client.DefaultRequestHeaders.Add("x-session-id", configuration["KEEPx-session-id"]);
        client.DefaultRequestHeaders.Add("Cookie", configuration["KEEPCookie"]);
        client.DefaultRequestHeaders.Add("x-user-id", configuration["KEEPx-user-id"]);
        client.DefaultRequestHeaders.Add("x-keep-timezone", configuration["KEEPx-keep-timezone"]);
        client.DefaultRequestHeaders.Add("Authorization", configuration["KEEPAuthorization"]);
        var response = client.GetAsync("https://api.gotokeep.com/pd/v3/stats/detail?dateUnit=all").Result;
        var result = response.Content.ReadAsStringAsync().Result;
        var resultModel = JsonConvert.DeserializeObject<KeepResponse>(result);
        if (resultModel is { ok: true, data.records: not null })
            foreach (var logitem in resultModel.data.records.SelectMany(item => item.logs ?? new List<DailyList>()))
            {
                if (logitem.stats == null) continue;
                dbConnection.Execute($@"delete from public.eventinfo where source = :source and distinguishingmark=:distinguishingmark",
                    new { source = "keep", distinguishingmark = logitem.stats.id });
                if (logitem.stats.type != "training")
                {
                    // 转换为TimeSpan 
                    var span = TimeSpan.FromMilliseconds(Math.Abs(logitem.stats.endTime - logitem.stats.startTime));
                    if (logitem.stats.doneDate != null)
                        dbConnection.Execute(
                            $@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                            , new
                            {
                                id = Guid.NewGuid().ToString(),
                                title = logitem.stats.name + logitem.stats.nameSuffix,
                                message = "用时 " + $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2};消耗 " + logitem.stats.calorie + "千卡",
                                clockintime = DateTime.Parse(logitem.stats.doneDate).ToString("yyyy-MM-dd HH:mm:ss"),
                                color = "green",
                                source = "keep",
                                distinguishingmark = logitem.stats.id
                            });
                }
                else
                {
                    var traresponse = client.GetAsync("https://api.gotokeep.com/minnow-webapp/v1/sportlog/" + logitem.stats.id).Result;
                    var traresult = traresponse.Content.ReadAsStringAsync().Result;
                    var traResultModel = JsonConvert.DeserializeObject<SportLogResponse>(traresult);
                    if (traResultModel is not { ok: true }) continue;
                    if (traResultModel.data?.sections != null)
                    {
                        var sportLogSectionsModel = traResultModel.data?.sections.FirstOrDefault(e => e.style?.ToLower() == "sportdata");
                        if (sportLogSectionsModel != null)
                            if (sportLogSectionsModel.content?.list != null)
                            {
                                var sportLogContentListTime = sportLogSectionsModel.content?.list.FirstOrDefault(e => e.title == "训练时长");
                                var sportLogContentListDistance = sportLogSectionsModel.content?.list.FirstOrDefault(e => e.title == "总距离");
                                if (sportLogContentListTime != null && sportLogContentListDistance != null)
                                    if (logitem.stats.doneDate != null)
                                        dbConnection.Execute(
                                            $@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                                            , new
                                            {
                                                id = Guid.NewGuid().ToString(),
                                                title = logitem.stats.name + logitem.stats.nameSuffix + sportLogContentListDistance.valueStr + sportLogContentListDistance.unit,
                                                message = "用时 " + sportLogContentListTime.valueStr + ";消耗 " + logitem.stats.calorie + "千卡",
                                                clockintime = DateTime.Parse(logitem.stats.doneDate).ToString("yyyy-MM-dd HH:mm:ss"),
                                                color = "green",
                                                source = "keep",
                                                distinguishingmark = logitem.stats.id
                                            });
                            }
                    }
                }
            }

        dbConnection.Dispose();
    }
}

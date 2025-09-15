using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Npgsql;
using FreeWim.Common;
using FreeWim.Models.PmisAndZentao;
using Newtonsoft.Json.Linq;

namespace FreeWim.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class MiFanController(
    IConfiguration configuration,
    ILogger<SpeedTestController> logger,
    ZentaoHelper zentaoHelper,
    AttendanceHelper attendanceHelper,
    PmisHelper pmisHelper,
    PushMessageHelper pushMessageHelper,
    IChatClient chatClient)
    : Controller
{
    private readonly IConfiguration _configuration = configuration;

    [ApiExplorerSettings(IgnoreApi = true)]
    [Tags("米饭公社")]
    [EndpointSummary("获取绑定信息")]
    [HttpGet]
    public async Task<string> Getbindings()
    {
        using var conn = new NpgsqlConnection(_configuration["Connection"]);
        await conn.OpenAsync();

        using var httpClient = new HttpClient();

        for (var accountId = 1; accountId <= 831029; accountId++)
            try
            {
                var url = $"https://mitemp.rwjservice.com/mifan/houses/getBindAccountWithHouseList?accountId={accountId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Authorization", "14278,0a97169b63db289fd67f7dbb471bac55");
                request.Headers.Add("User-Agent", "Mozilla/5.0");
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("data", out var data)) continue;
                if (!data.TryGetProperty("bindList", out var bindList)) continue;

                foreach (var item in bindList.EnumerateArray())
                {
                    var cmd = new NpgsqlCommand(@"
                        INSERT INTO house_bindings 
                        (houseWechatId, accountId, houseId, active, mobile, bindTagRoleName, bindTagRoleId, tag, communityId, city, communityName, block, unit, room, inviteBind, ownerName, userBind, takeoverState, realestateImgUrl, realestateServicesLinkUrl, startDate, endDate) 
                        VALUES (@houseWechatId, @accountId, @houseId, @active, @mobile, @bindTagRoleName, @bindTagRoleId, @tag, @communityId, @city, @communityName, @block, @unit, @room, @inviteBind, @ownerName, @userBind, @takeoverState, @realestateImgUrl, @realestateServicesLinkUrl, @startDate, @endDate)
                    ", conn);

                    cmd.Parameters.AddWithValue("@houseWechatId", (object?)item.GetProperty("houseWechatId").GetInt64());
                    cmd.Parameters.AddWithValue("@accountId", (object?)item.GetProperty("accountId").GetInt64());
                    cmd.Parameters.AddWithValue("@houseId", (object?)item.GetProperty("houseId").GetInt64());
                    cmd.Parameters.AddWithValue("@active", (object?)item.GetProperty("active").GetInt32());
                    cmd.Parameters.AddWithValue("@mobile", (object?)item.GetProperty("mobile").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@bindTagRoleName", (object?)item.GetProperty("bindTagRoleName").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@bindTagRoleId", (object?)item.GetProperty("bindTagRoleId").GetInt32());
                    cmd.Parameters.AddWithValue("@tag", (object?)item.GetProperty("tag").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@communityId", (object?)item.GetProperty("communityId").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@city", (object?)item.GetProperty("city").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@communityName", (object?)item.GetProperty("communityName").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@block", (object?)item.GetProperty("block").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@unit", (object?)item.GetProperty("unit").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@room", (object?)item.GetProperty("room").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@inviteBind", (object?)item.GetProperty("inviteBind").GetInt32());
                    cmd.Parameters.AddWithValue("@ownerName", (object?)item.GetProperty("ownerName").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@userBind", (object?)item.GetProperty("userBind").GetInt32());
                    cmd.Parameters.AddWithValue("@takeoverState", (object?)item.GetProperty("takeoverState").GetInt32());
                    cmd.Parameters.AddWithValue("@realestateImgUrl", (object?)item.GetProperty("realestateImgUrl").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@realestateServicesLinkUrl", (object?)item.GetProperty("realestateServicesLinkUrl").GetString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@startDate", DBNull.Value); // API返回null
                    cmd.Parameters.AddWithValue("@endDate", DBNull.Value); // API返回null

                    await cmd.ExecuteNonQueryAsync();
                }

                Console.WriteLine($"✅ Inserted data for accountId={accountId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error at accountId={accountId}: {ex.Message}");
            }

        return "";
    }

//     [Tags("米饭公社")]
//     [EndpointSummary("获取用户信息")]
//     [HttpGet]
//     public async Task<string> GetZentaoToken()
//     {
//         var BaseUrl = "https://mitemp.rwjservice.com/mifan/accounts/";
//         var AuthHeader = "14278,0a97169b63db289fd67f7dbb471bac55";
//         using var client = new HttpClient();
//         client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", AuthHeader);
//         client.DefaultRequestHeaders.Add("Accept", "application/json");
//
//         for (long id = 800001; id <= 831029; id++)
//         {
//             var url = BaseUrl + id;
//             try
//             {
//                 var response = await client.GetAsync(url);
//                 if (!response.IsSuccessStatusCode)
//                 {
//                     Console.WriteLine($"⚠️ ID {id} returned {response.StatusCode}");
//                     continue;
//                 }
//
//                 var json = await response.Content.ReadAsStringAsync();
//                 var root = JsonNode.Parse(json);
//                 var item = root?["item"];
//
//                 if (item == null)
//                 {
//                     Console.WriteLine($"⚠️ ID {id} has no 'item'");
//                     continue;
//                 }
//
//                 await InsertToPostgres(item);
//                 Console.WriteLine($"✅ ID {id} inserted.");
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"❌ ID {id} error: {ex.Message}");
//             }
//         }
//
//         return "";
//     }
//
//     private async Task InsertToPostgres(JsonNode item)
//     {
//         using var conn = new NpgsqlConnection(_configuration["Connection"]);
//         await conn.OpenAsync();
//
//         var sql = @"
// INSERT INTO account_data(
//     id, rowState, createDate, mobile, nickName, headImgUrl, idNo, hideAge, openid, name, type, backup, sex, flag, authFlag,
//     masterPhone, score, registerFlag, userCode, szUserCode, token, age, average, validity, orderCount, password, channelId,
//     registrationId, loginPassword, inviter, tradePassword, appletOpenId, brokerId, isLogin, onLogin, firstLoginTime, source,
//     house, masterCommunityId, isBindHouse, balancePassword, balanceAmount, integral, odyFlag, loginOdyData, gzhOpenId,
//     yzOpenId, icons, image, lastModifiedDate
// )
// VALUES(
//     @id, @rowState, @createDate, @mobile, @nickName, @headImgUrl, @idNo, @hideAge, @openid, @name, @type, @backup, @sex, @flag, @authFlag,
//     @masterPhone, @score, @registerFlag, @userCode, @szUserCode, @token, @age, @average, @validity, @orderCount, @password, @channelId,
//     @registrationId, @loginPassword, @inviter, @tradePassword, @appletOpenId, @brokerId, @isLogin, @onLogin, @firstLoginTime, @source,
//     @house, @masterCommunityId, @isBindHouse, @balancePassword, @balanceAmount, @integral, @odyFlag, @loginOdyData, @gzhOpenId,
//     @yzOpenId, @icons, @image, @lastModifiedDate
// )
// ON CONFLICT (id) DO NOTHING;
// ";
//
//         using var cmd = new NpgsqlCommand(sql, conn);
//
//         // 所有字段映射
//         cmd.Parameters.AddWithValue("id", (object?)item["id"]?.GetValue<long>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("rowState", (object?)item["rowState"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("createDate", (object?)ParseDateTime(item["createDate"]?.ToString()));
//         cmd.Parameters.AddWithValue("mobile", (object?)item["mobile"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("nickName", (object?)item["nickName"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("headImgUrl", (object?)item["headImgUrl"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("idNo", (object?)item["idNo"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("hideAge", (object?)item["hideAge"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("openid", (object?)item["openid"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("name", (object?)item["name"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("type", (object?)item["type"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("backup", (object?)item["backup"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("sex", (object?)item["sex"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("flag", (object?)item["flag"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("authFlag", (object?)item["authFlag"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("masterPhone", (object?)item["masterPhone"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("score", (object?)item["score"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("registerFlag", (object?)item["registerFlag"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("userCode", (object?)item["userCode"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("szUserCode", (object?)item["szUserCode"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("token", (object?)item["token"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("age", (object?)item["age"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("average", (object?)item["average"]?.GetValue<double>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("validity", (object?)item["validity"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("orderCount", (object?)item["orderCount"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("password", (object?)item["password"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("channelId", (object?)item["channelId"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("registrationId", (object?)item["registrationId"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("loginPassword", (object?)item["loginPassword"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("inviter", (object?)item["inviter"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("tradePassword", (object?)item["tradePassword"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("appletOpenId", (object?)item["appletOpenId"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("brokerId", (object?)item["brokerId"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("isLogin", (object?)item["isLogin"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("onLogin", (object?)item["onLogin"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("firstLoginTime", (object?)ParseDateTime(item["firstLoginTime"]?.ToString()));
//         cmd.Parameters.AddWithValue("source", (object?)item["source"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("house", (object?)item["house"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("masterCommunityId", (object?)item["masterCommunityId"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("isBindHouse", (object?)item["isBindHouse"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("balancePassword", (object?)item["balancePassword"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("balanceAmount", (object?)item["balanceAmount"]?.GetValue<double>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("integral", (object?)item["integral"]?.GetValue<int>() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("odyFlag", (object?)item["odyFlag"]?.GetValue<int>() ?? DBNull.Value);
//         // cmd.Parameters.AddWithValue("coupons", (object?)JsonSerializer.Serialize(item["coupons"] ?? "[]"));
//         cmd.Parameters.AddWithValue("loginOdyData", (object?)item["loginOdyData"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("gzhOpenId", (object?)item["gzhOpenId"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("yzOpenId", (object?)item["yzOpenId"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("icons", (object?)item["icons"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("image", (object?)item["image"]?.ToString() ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("lastModifiedDate", (object?)ParseDateTime(item["lastModifiedDate"]?.ToString()));
//
//         await cmd.ExecuteNonQueryAsync();
//     }
//
//     private static object? ParseDateTime(string? str)
//     {
//         if (string.IsNullOrEmpty(str)) return DBNull.Value;
//         if (DateTime.TryParse(str, out var dt))
//             return dt;
//         return DBNull.Value;
//     }
}
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FreeWim.Models.PmisAndZentao;

namespace FreeWim.Common;

public class TokenService(IConfiguration configuration)
{
    private const string TokenCacheKey = "AuthToken";
    private const int TokenExpirationDuration = 24; // 24 hours
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public string GetTokenAsync()
    {
        // Try to get the token from memory cache first
        if (_cache.TryGetValue(TokenCacheKey, out string cachedToken)) return cachedToken;

        // If token is not found in cache, fetch a new one
        var token = FetchTokenFromApiAsync();

        // Cache the token with expiration time (24 hours)
        _cache.Set(TokenCacheKey, token, TimeSpan.FromHours(TokenExpirationDuration));

        return token;
    }

    private string FetchTokenFromApiAsync()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var token = string.Empty;
        // var userName = "925";
        // var passWord = "925123!";
        // var uniwaterUrl = "";
        var httpClient = new HttpClient();

        #region 获取统一平台RSA密钥

        var rsAbuffer = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
        {
        }));
        var rsaByteContent = new ByteArrayContent(rsAbuffer);
        rsaByteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var rsaResponse = httpClient.GetAsync(pmisInfo.DlmeasureUrl + "/uniwim/ump/key").Result;
        var projectJson = JObject.Parse(rsaResponse.Content.ReadAsStringAsync().Result);
        var publicKeyClean = projectJson["Response"]["publicKey"].ToString().Replace("-----BEGIN RSA Public Key-----", "")
            .Replace("-----END RSA Public Key-----", "")
            .Replace("\n", "")
            .Replace("\r", "");

        #endregion

        #region 处理密码加密

        var publicKeyBytes =
            Convert.FromBase64String(publicKeyClean);
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(pmisInfo.PassWord);
        var encryptedBytes = rsa.Encrypt(plaintextBytes, RSAEncryptionPadding.Pkcs1);

        #endregion

        #region 登录

        var loginData = JsonConvert.SerializeObject(new
        {
            username = pmisInfo.UserAccount,
            password = EnCode(Convert.ToBase64String(encryptedBytes)),
            pwdForRemember = EnCode(pmisInfo.PassWord),
            validation = "",
            cid = "",
            cfg = "",
            appgroup = "",
            mac = "",
            tenantName = "和达科技",
            tenantId = "5d89917712441d7a5073058c"
        });
        var response = httpClient.PostAsync(pmisInfo.DlmeasureUrl + "/uniwim/dmp/login", new StringContent(JsonConvert.SerializeObject(new
        {
            data = EnCode(loginData)
        }))).Result;
        if (!response.IsSuccessStatusCode) return token;
        var tokenObject = JObject.Parse(response.Content.ReadAsStringAsync().Result);
        token = tokenObject["Response"]["token"].ToString();

        #endregion

        return token;
    }

    public string EnCode(string input)
    {
        //chunk-cue.125e90c9.js
        var script = @"
            function enc(e) {
                        var charMap = ""NjCG7lX9WbVtnaA1TxzEY5OpuJ8Pr4oZF3s-SKdkchv2mqyLiD0efwRIBH_=6UgMQ"";
                        for (var t, n, i = String(e), r = charMap, o = 0, a = """", s = 3 / 4; !isNaN(t = i.charCodeAt(s)) || 63 & o || (r = ""Q"",
                        (s - 3 / 4) % 1); s += 3 / 4)
                            if (t > 127) {
                                (n = encodeURI(i.charAt(s)).split(""%"")).shift();
                                for (var l, u = s % 1; l = n[0 | u]; u += 3 / 4)
                                    o = o << 8 | parseInt(l, 16),
                                    a += r.charAt(63 & o >> 8 - u % 1 * 8);
                                s = s === 3 / 4 ? 0 : s,
                                s += 3 / 4 * n.length % 1
                            } else
                                o = o << 8 | t,
                                a += r.charAt(63 & o >> 8 - s % 1 * 8);
                        return a
                    }
        ";
        var result = new Jint.Engine().Execute(script).Invoke("enc", input).ToString();
        return result;
    }
}
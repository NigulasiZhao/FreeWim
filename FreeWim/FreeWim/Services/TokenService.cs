using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FreeWim.Models.PmisAndZentao;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FreeWim.Services;

public class TokenService(IConfiguration configuration)
{
    private const string AdminTokenCacheKey = "AdminToken";
    private const string TokenCacheKey = "AuthToken";
    private const string AsusRouterTokenCacheKey = "AsusToken";
    private const int TokenExpirationDuration = 24; // 24 hours
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public string? GetTokenAsync()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        // Try to get the token from memory cache first
        if (_cache.TryGetValue(TokenCacheKey, out string? cachedToken)) return cachedToken;

        // If token is not found in cache, fetch a new one
        var token = FetchTokenFromApiAsync(pmisInfo.DlmeasureUrl, pmisInfo.UserAccount, pmisInfo.PassWord);

        // Cache the token with expiration time (24 hours)
        _cache.Set(TokenCacheKey, token, TimeSpan.FromHours(TokenExpirationDuration));

        return token;
    }

    public string? GetAdminTokenAsync()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        // Try to get the token from memory cache first
        if (_cache.TryGetValue(AdminTokenCacheKey, out string? cachedToken)) return cachedToken;

        // If token is not found in cache, fetch a new one
        var token = FetchTokenFromApiAsync(pmisInfo.DlmeasureUrl, "720", "720123!");

        // Cache the token with expiration time (24 hours)
        _cache.Set(AdminTokenCacheKey, token, TimeSpan.FromHours(TokenExpirationDuration));

        return token;
    }

    private string? FetchTokenFromApiAsync(string dlmeasureUrl, string userAccount, string passWord)
    {
        var token = string.Empty;
        var httpClient = new HttpClient();

        #region 获取统一平台RSA密钥

        var rsAbuffer = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
        {
        }));
        var rsaByteContent = new ByteArrayContent(rsAbuffer);
        rsaByteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var rsaResponse = httpClient.GetAsync(dlmeasureUrl + "/uniwim/ump/key").Result;
        var projectJson = JObject.Parse(rsaResponse.Content.ReadAsStringAsync().Result);
        var publicKeyClean = projectJson["Response"]?["publicKey"]
            ?.ToString().Replace("-----BEGIN RSA Public Key-----", "")
            .Replace("-----END RSA Public Key-----", "")
            .Replace("\n", "")
            .Replace("\r", "");

        #endregion

        #region 处理密码加密

        if (publicKeyClean != null)
        {
            var publicKeyBytes =
                Convert.FromBase64String(publicKeyClean);
            var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(passWord);
            var encryptedBytes = rsa.Encrypt(plaintextBytes, RSAEncryptionPadding.Pkcs1);

            #endregion

            #region 登录

            var loginData = JsonConvert.SerializeObject(new
            {
                username = userAccount,
                password = EnCode(Convert.ToBase64String(encryptedBytes)),
                pwdForRemember = EnCode(passWord),
                validation = "",
                cid = "",
                cfg = "",
                appgroup = "",
                mac = "",
                tenantName = "和达科技",
                tenantId = "5d89917712441d7a5073058c"
            });
            var response = httpClient.PostAsync(dlmeasureUrl + "/uniwim/dmp/login", new StringContent(JsonConvert.SerializeObject(new
            {
                data = EnCode(loginData)
            }))).Result;
            if (!response.IsSuccessStatusCode) return token;
            var tokenObject = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            token = tokenObject["Response"]?["token"]?.ToString();
        }

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
    
    public string? GetAsusRouterTokenAsync()
    {
        //var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
        // Try to get the token from memory cache first
        if (_cache.TryGetValue(AsusRouterTokenCacheKey, out string? cachedToken)) return cachedToken;

        // If token is not found in cache, fetch a new one
        var token = FetchTokenFromAsusRouter();

        _cache.Set(AsusRouterTokenCacheKey, token, TimeSpan.FromMinutes(15));

        return token;
    }
    
    public string? FetchTokenFromAsusRouter()
    {
        var baseUri = new Uri("http://192.168.50.1");

        // 1. 初始化 Cookie 容器
        var cookieContainer = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            // 如果你的路由器使用了自签名 HTTPS 证书，请取消下面这行的注释
            // ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true 
        };

        using var client = new HttpClient(handler);

        // 2. 设置全局 Default Request Headers (对应你 curl 中的大部分 header)
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
        client.DefaultRequestHeaders.Add("Connection", "keep-alive");
        client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");

        // 特别注意：Referer 非常重要，华硕固件通常会校验它
        client.DefaultRequestHeaders.Add("Referer", "http://192.168.50.1/Main_Login.asp");
        client.DefaultRequestHeaders.Add("Origin", "http://192.168.50.1");

        // 3. 准备 POST 表单数据
        var formData = new Dictionary<string, string>
        {
            { "group_id", "" },
            { "action_mode", "" },
            { "action_script", "" },
            { "action_wait", "5" },
            { "current_page", "Main_Login.asp" },
            { "next_page", "index.asp" },
            { "login_authorization", "MTUyOTAwMzIxMjA6WkhBT2xpYW5nLjE5OTQ=" }, // 替换为你的 base64 认证串
            { "login_captcha", "" }
        };
        var content = new FormUrlEncodedContent(formData);

        try
        {
            // 4. 发送 POST 请求
            HttpResponseMessage response = client.PostAsync("http://192.168.50.1/login.cgi", content).Result;

            // 确保请求成功
            response.EnsureSuccessStatusCode();

            // 5. 从 CookieContainer 中获取 asus_token
            var cookies = cookieContainer.GetCookies(baseUri);
            string asusToken = string.Empty;

            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name.Equals("asus_token", StringComparison.OrdinalIgnoreCase))
                {
                    asusToken = cookie.Value;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(asusToken))
            {
                return asusToken;
            }
            else
            {
                return null;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}
using System.Text;
using System.Text.Json;
using System.Web;

namespace FreeWim.Utils;

public class HttpRequestHelper
{
    private readonly HttpClient _httpClient;

    public HttpRequestHelper(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    public async Task<HttpResponseMessage> GetAsync(string url, Dictionary<string, string>? headers = null, Dictionary<string, string>? queryParams = null)
    {
        var finalUrl = BuildUrl(url, queryParams);
        var request = new HttpRequestMessage(HttpMethod.Get, finalUrl);
        AddHeaders(request, headers);

        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// 发送 POST 请求，支持 JSON Body
    /// </summary>
    public async Task<HttpResponseMessage> PostAsync<T>(string url, T body, Dictionary<string, string>? headers = null, Dictionary<string, string>? queryParams = null)
    {
        var finalUrl = BuildUrl(url, queryParams);
        var request = new HttpRequestMessage(HttpMethod.Post, finalUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        AddHeaders(request, headers);

        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// 发送 POST 请求，支持 JSON Body
    /// </summary>
    public async Task<HttpResponseMessage> PostAsyncStringBody(string url, string? body, Dictionary<string, string>? headers = null, Dictionary<string, string>? queryParams = null)
    {
        var finalUrl = BuildUrl(url, queryParams);
        var request = new HttpRequestMessage(HttpMethod.Post, finalUrl)
        {
            Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json")
        };
        AddHeaders(request, headers);

        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// 构建 URL 带 query 参数
    /// </summary>
    private static string BuildUrl(string baseUrl, Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || !queryParams.Any())
            return baseUrl;

        var builder = new UriBuilder(baseUrl);
        var query = HttpUtility.ParseQueryString(builder.Query);
        foreach (var kv in queryParams) query[kv.Key] = kv.Value;

        builder.Query = query.ToString();
        return builder.ToString();
    }

    /// <summary>
    /// 添加 Headers 到请求中
    /// </summary>
    private static void AddHeaders(HttpRequestMessage request, Dictionary<string, string>? headers)
    {
        if (headers == null) return;

        foreach (var header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }
}
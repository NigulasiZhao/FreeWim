using System.Net.WebSockets;
using System.Text;
using FreeWim.Models.PmisAndZentao;
using FreeWim.Utils;
using Newtonsoft.Json;

namespace FreeWim.Services;

public class YhloWebSocketService(IConfiguration configuration, TokenService tokenService, JwtService jwtService)
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isConnected;
    private string? _cachedMainId; // 缓存mainId

    /// <summary>
    /// 初始化WebSocket连接并发送认证消息
    /// </summary>
    public async Task InitializeConnection()
    {
        try
        {
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
            var wsUrl = pmisInfo.Url?.Replace("https://", "ws://").Replace("http://", "ws://") + "/yhlo";
            var token = tokenService.GetTokenAsync();

            // 使用JwtHelper解析token获取mainId
            var mainId = jwtService.GetMainId(token) ?? "";
            if (!string.IsNullOrEmpty(mainId)) _cachedMainId = mainId;

            // 如果已经连接，先断开
            if (_isConnected && _webSocket?.State == WebSocketState.Open) return;

            // 清理旧连接
            await DisconnectAsync();

            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            await _webSocket.ConnectAsync(new Uri(wsUrl), _cancellationTokenSource.Token);

            _isConnected = true;

            // 发送认证消息
            var authMessage = new
            {
                messageType = 0,
                userId = pmisInfo.UserId,
                token
            };

            await SendMessageAsync(authMessage);

            // 启动接收消息的后台任务
            _ = Task.Run(async () => await ReceiveMessagesAsync());
        }
        catch (Exception)
        {
            _isConnected = false;
            throw;
        }
    }

    /// <summary>
    /// 发送心跳消息
    /// </summary>
    public async Task SendHeartbeat()
    {
        try
        {
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;

            // 检查连接状态
            if (!_isConnected || _webSocket?.State != WebSocketState.Open)
            {
                await InitializeConnection();
                return;
            }

            var heartbeatMessage = new
            {
                ping = pmisInfo.UserMobile,
                pingInfo = new
                {
                    mobile = pmisInfo.UserMobile,
                    mainId = _cachedMainId ?? ""
                }
            };

            await SendMessageAsync(heartbeatMessage);
        }
        catch (Exception)
        {
            _isConnected = false;
        }
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    private async Task SendMessageAsync(object message)
    {
        if (_webSocket?.State != WebSocketState.Open) throw new InvalidOperationException("WebSocket未连接");

        var json = JsonConvert.SerializeObject(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var buffer = new ArraySegment<byte>(bytes);

        await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, _cancellationTokenSource?.Token ?? CancellationToken.None);
    }

    /// <summary>
    /// 接收消息的后台任务
    /// </summary>
    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[1024 * 4];

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !(_cancellationTokenSource?.Token.IsCancellationRequested ?? true))
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource!.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _isConnected = false;
                    break;
                }

                Encoding.UTF8.GetString(buffer, 0, result.Count);
            }
        }
        catch (Exception)
        {
            _isConnected = false;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();

            if (_webSocket?.State == WebSocketState.Open) await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "关闭连接", CancellationToken.None);

            _webSocket?.Dispose();
            _cancellationTokenSource?.Dispose();

            _webSocket = null;
            _cancellationTokenSource = null;
            _isConnected = false;
        }
        catch (Exception)
        {
            // ignored
        }
    }

    /// <summary>
    /// 刷新在线状态
    /// 每日执行一次，更新用户在线状态
    /// </summary>
    public async Task RefreshOnlineStatus()
    {
        try
        {
            var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>()!;
            var token = tokenService.GetTokenAsync();
            var url = pmisInfo.Url.TrimEnd('/') + "/uniwim/message/userOnLineOrOffLine/updateUserState?type=2&state=onLine";

            var httpHelper = new HttpRequestHelper();
            if (token != null)
            {
                var headers = new Dictionary<string, string>
                {
                    { "uniwater_utoken", token }
                };

                await httpHelper.GetAsync(url, headers);
            }
        }
        catch (Exception)
        {
            // 记录异常但不影响其他流程
        }
    }
}
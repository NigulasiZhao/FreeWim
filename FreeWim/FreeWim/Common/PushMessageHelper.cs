using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;
using Newtonsoft.Json;
using FreeWim.Models.Email;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace FreeWim.Common;

public class PushMessageHelper(IConfiguration configuration)
{
    /// <summary>
    /// 消息推送
    /// </summary>
    /// <param name="title">推送标题</param>
    /// <param name="message">推送信息</param>
    /// <param name="icon">推送图标</param>
    /// <param name="customIconUrl">自定义图标地址</param>
    public void Push(string title, string message, PushIcon icon = PushIcon.Default, string customIconUrl = null)
    {
        try
        {
            var pushInfoList = configuration.GetSection("PushInfo").Get<List<PushInfo>>();
            if (pushInfoList == null) return;
            var iconUrl = GetIconUrl(icon, customIconUrl);
            foreach (var pushItem in pushInfoList)
            {
                if (pushItem.PushType.Equals("bark", StringComparison.CurrentCultureIgnoreCase))
                {
                    var barkclient = new HttpClient();
                    var data = JsonConvert.SerializeObject(new
                    {
                        body = message,
                        title = title,
                        badge = 1,
                        icon = iconUrl,
                        group = ""
                    });
                    var byteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(data));
                    byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    barkclient.PostAsync(pushItem.PushUrl, byteContent);
                }

                if (pushItem.PushType.Equals("gotify", StringComparison.CurrentCultureIgnoreCase))
                {
                    using var gotifyclient = new HttpClient();
                    using var formData = new MultipartFormDataContent();
                    formData.Add(new StringContent(title), "title");
                    formData.Add(new StringContent(message), "message");
                    formData.Add(new StringContent(configuration["PushMessagePriority"]!), "priority");

                    var response = gotifyclient.PostAsync(pushItem.PushUrl, formData).Result;
                    response.EnsureSuccessStatusCode();
                    var responseBody = response.Content.ReadAsStringAsync().Result;
                }

                if (pushItem.PushType.Equals("ntfy", StringComparison.CurrentCultureIgnoreCase))
                {
                    var ntfyclient = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Put, pushItem.PushUrl + $"?title={title}&message={message}");
                    request.Headers.Add("Priority", "5");
                    request.Headers.Add("icon", iconUrl);
                    var content = new StringContent("", null, "text/plain");
                    request.Content = content;
                    var response = ntfyclient.SendAsync(request).Result;
                    response.EnsureSuccessStatusCode();
                    var responseBody = response.Content.ReadAsStringAsync();
                }

                if (!pushItem.PushType.Equals("email", StringComparison.CurrentCultureIgnoreCase)) continue;
                var emailInfo = configuration.GetSection("EmaliInfo").Get<EmaliInfo>();
                if (emailInfo == null) continue;
                var sendmessage = new MimeMessage
                {
                    Subject = title,
                    Body = new BodyBuilder
                    {
                        HtmlBody = message
                    }.ToMessageBody()
                };
                var addressList = emailInfo.ReceiveList
                    .Select(r => new MailboxAddress(r.Name, r.Address))
                    .ToList();

                sendmessage.From.Add(new MailboxAddress(Encoding.UTF8, title, emailInfo.UserName));
                sendmessage.To.AddRange(addressList);

                using var client = new SmtpClient
                {
                    ServerCertificateValidationCallback = (s, c, h, e) => true
                };
                client.AuthenticationMechanisms.Remove("XOAUTH2");

                client.Connect(emailInfo.Host, emailInfo.Port, emailInfo.UseSsl);
                client.Authenticate(emailInfo.UserName, emailInfo.PassWord);
                client.Send(sendmessage);
                client.Disconnect(true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    private string GetIconUrl(PushIcon icon, string? customUrl = null)
    {
        return icon switch
        {
            PushIcon.Default => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/netcam-studio.png",
            PushIcon.Camera => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/netcam-studio.png",
            PushIcon.Zentao => "https://is3-ssl.mzstatic.com/image/thumb/Purple124/v4/02/1c/18/021c18a3-7116-0193-263f-5634a8b28247/AppIcon-0-1x_U007emarketing-0-0-85-220-7.png/320x0w.png",
            PushIcon.Note => "https://www.dlmeasure.com/uniwim/uploads/2025/6/e38b279f59504ef09e5615a8f78f1643.png",
            PushIcon.OverTime => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/wakatime-light.png",
            PushIcon.Alert => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/backblaze.png",
            PushIcon.Attendance => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/airvpn.png",
            PushIcon.DeepSeek => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/bitcoin.png",
            _ => ""
        };
    }

    public enum PushIcon
    {
        Default, // 默认图标
        Camera, // 摄像头
        Zentao,
        Note,
        OverTime,
        Alert,
        Attendance,
        DeepSeek
    }
}
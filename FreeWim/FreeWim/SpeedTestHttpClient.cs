using System.Runtime.InteropServices;

namespace FreeWim;

internal class SpeedTestHttpClient : HttpClient
{
    public int ConnectionLimit { get; set; }

    public SpeedTestHttpClient()
    {
        var frameworkInfo = RuntimeInformation.FrameworkDescription.Split();
        var frameworkName = $"{frameworkInfo[0]}{frameworkInfo[1]}";

        var osInfo = RuntimeInformation.OSDescription.Split();

        DefaultRequestHeaders.Add("Accept", "text/html, application/xhtml+xml, */*");
        DefaultRequestHeaders.Add("User-Agent", string.Join(" ", new string[]
        {
            "Mozilla/5.0",
            $"({osInfo[0]}-{osInfo[1]}; U; {RuntimeInformation.ProcessArchitecture}; en-us)",
            $"{frameworkName}/{frameworkInfo[2]}",
            "(KHTML, like Gecko)",
            $"SpeedTest.Net/1.4.0.0"
        }));
    }
}
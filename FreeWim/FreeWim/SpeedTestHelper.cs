using FreeWim.Models;
using FreeWim.SpeedTest;

namespace FreeWim;

public class SpeedTestHelper
{
    private static SpeedTestClient client;
    private static Settings settings;

    public Server StartSpeedTest()
    {
        client = new SpeedTestClient();
        settings = client.GetSettings();
        SelectServers();
        var bestServer = SelectBestServer(settings.Servers);

        var downloadSpeed = client.TestDownloadSpeed(bestServer, settings.Download.ThreadsPerUrl);
        var uploadSpeed = client.TestUploadSpeed(bestServer, settings.Upload.ThreadsPerUrl);
        bestServer.downloadSpeed = Math.Round(downloadSpeed / 1024, 2);
        bestServer.uploadSpeed = Math.Round(uploadSpeed / 1024, 2);
        return bestServer;
    }

    private static IEnumerable<Server> SelectServers()
    {
        Console.WriteLine();
        Console.WriteLine("Selecting best server by distance...");
        var servers = settings.Servers.Where(e => e.Country == "China").Take(10).ToList();

        foreach (var server in servers) server.Latency = client.TestServerLatency(server);
        return servers;
    }

    private static Server SelectBestServer(IEnumerable<Server> servers)
    {
        var bestServer = servers.OrderBy(x => x.Latency).First();
        return bestServer;
    }
}
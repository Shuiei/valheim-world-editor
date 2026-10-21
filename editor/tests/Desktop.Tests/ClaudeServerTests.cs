using System.Net;
using System.Net.Sockets;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's server as Settings turns it on and off (ClaudeServer.Apply): a token made the first time and saved, the
/// server listening, started again on another port, a port already taken said, and stopped when the switch is off.
/// Its settings file is the tests' own, so this runs alone.
/// </summary>
[Collection("DataDir")]
public class ClaudeServerTests
{
    /// <summary>A port free on this computer now.</summary>
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [AvaloniaFact]
    public async Task SettingsStartAndStopTheServer()
    {
        var w = new MainWindow(load: false);
        var settings = new AppSettings { ClaudeConnect = true, ClaudePort = FreePort() };
        try
        {
            Assert.Null(await ClaudeServer.Apply(settings, w));
            Assert.Equal(48, settings.ClaudeToken!.Length);
            Assert.Equal($"Listening on {ClaudeServer.Url(settings.ClaudePort)}", ClaudeServer.Status);
            string token = settings.ClaudeToken;
            using (var http = new HttpClient())
            {
                var answer = await http.PostAsync(ClaudeServer.Url(settings.ClaudePort), new StringContent("{}"), TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode);
            }

            // Started again on another port, with the same token: the first one stopped.
            int first = settings.ClaudePort;
            settings.ClaudePort = FreePort();
            Assert.Null(await ClaudeServer.Apply(settings, w));
            Assert.Equal(token, settings.ClaudeToken);
            Assert.Equal($"Listening on {ClaudeServer.Url(settings.ClaudePort)}", ClaudeServer.Status);
            using (var http = new HttpClient())
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => http.PostAsync(ClaudeServer.Url(first), new StringContent("{}"), TestContext.Current.CancellationToken));
            }

            // A port another program holds: said, and nothing listening.
            using var taken = new TcpListener(IPAddress.Loopback, 0);
            taken.Start();
            settings.ClaudePort = ((IPEndPoint)taken.LocalEndpoint).Port;
            string? problem = await ClaudeServer.Apply(settings, w);
            Assert.StartsWith($"Could not listen on port {settings.ClaudePort}: ", problem);
            Assert.Equal(problem, ClaudeServer.Status);
        }
        finally
        {
            settings.ClaudeConnect = false;
            Assert.Null(await ClaudeServer.Apply(settings, w));
        }
        Assert.Null(ClaudeServer.Status);
    }

    // The desktop app's configuration: the editor itself, relaying with --mcp-stdio.
    [Fact]
    public void TheDesktopConfigurationRunsTheEditorAsARelay()
    {
        var config = System.Text.Json.JsonDocument.Parse(ClaudeServer.DesktopConfig("/opt/vwe/ValheimWorldEditor")).RootElement;
        var server = config.GetProperty("mcpServers").GetProperty("valheim-editor");
        Assert.Equal("/opt/vwe/ValheimWorldEditor", server.GetProperty("command").GetString());
        Assert.Equal("--mcp-stdio", server.GetProperty("args")[0].GetString());
    }
}

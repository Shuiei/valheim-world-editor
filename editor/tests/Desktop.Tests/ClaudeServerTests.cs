using System.Net;
using System.Net.Sockets;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using WorldEditor.Tests;
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
    [AvaloniaFact]
    public async Task SettingsStartAndStopTheServer()
    {
        var w = new MainWindow(load: false);
        var settings = new AppSettings { ClaudeConnect = true, ClaudePort = LoopbackPort.Free() };
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
            settings.ClaudePort = LoopbackPort.Free();
            Assert.Null(await ClaudeServer.Apply(settings, w));
            Assert.Equal(token, settings.ClaudeToken);
            Assert.Equal($"Listening on {ClaudeServer.Url(settings.ClaudePort)}", ClaudeServer.Status);
            using (var http = new HttpClient())
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => http.PostAsync(ClaudeServer.Url(first), new StringContent("{}"), TestContext.Current.CancellationToken));
            }

            // A port another program holds: said with the address (a network address can be gone too), and nothing
            // listening.
            using var taken = new TcpListener(IPAddress.Loopback, 0);
            taken.Start();
            settings.ClaudePort = ((IPEndPoint)taken.LocalEndpoint).Port;
            string? problem = await ClaudeServer.Apply(settings, w);
            Assert.StartsWith($"Could not listen on 127.0.0.1:{settings.ClaudePort}: ", problem);
            Assert.Equal(problem, ClaudeServer.Status);

            // An address that is not one (a settings file written by hand): said, and nothing listening.
            settings.ClaudePort = LoopbackPort.Free();
            settings.ClaudeAddress = "my-pc.local";
            problem = await ClaudeServer.Apply(settings, w);
            Assert.StartsWith("Could not listen on my-pc.local: not an IP address", problem);
            Assert.Equal(problem, ClaudeServer.Status);
            // Nor one written short: "0" would be 0.0.0.0, every network.
            settings.ClaudeAddress = "0";
            Assert.StartsWith("Could not listen on 0: not an IP address", await ClaudeServer.Apply(settings, w));
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

    // The server's errors that carry an exception (a tool that threw) go into the editor's log; the rest does not.
    [Fact]
    public void ErrorsWithAnExceptionGoIntoTheLog()
    {
        using var log = Log.Listen();
        using var errors = new ClaudeServer.ErrorsToLog();
        var logger = errors.CreateLogger("ModelContextProtocol");
        Assert.Null(logger.BeginScope("call"));
        static string Format(string state, Exception? _) => state;
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Warning, 0, "a warning", new InvalidOperationException("no"), Format);
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Error, 0, "no exception", null, Format);
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Error, 0, "\"place_pieces\" threw an unhandled exception.", new InvalidOperationException("gone"), Format);
        string line = Assert.Single(log.Lines);
        Assert.Contains("while serving Claude: \"place_pieces\" threw an unhandled exception: System.InvalidOperationException: gone", line);
    }
}

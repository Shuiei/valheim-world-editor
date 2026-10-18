using System.Net;
using System.Net.Sockets;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The tunnel's plugin side: the plugin's port read from the server's game folder, every diagnosis of a plugin that does
/// not answer, the server check not holding the caller's thread, and the stand-in plugin's disposal.
/// </summary>
[Collection("DataDir")]
public sealed class TunnelPluginTests : TunnelTestBase, IClassFixture<LocalSshd>
{
    public TunnelPluginTests(LocalSshd ssh) : base(ssh)
    {
    }

    [Theory]
    [InlineData(LocalGame.ConfigName, false)]
    [InlineData(LocalGame.OldConfigName, true)]
    public async Task ThePluginsPortIsReadFromTheServersGameFolder(string config, bool slash)
    {
        Ssh.Need();
        string game = Path.Combine(Dir, "it's a server");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "config"));
        string file = Path.Combine(game, "BepInEx", "config", Path.GetFileName(config));
        File.WriteAllText(file, $"[Bridge]\nToken = never read\n Port = {BridgePort}\n");
        var r = await Tunnel.Start(Req(folder: slash ? game + "/ " : game));
        Assert.Null(r.Error);
        Assert.Equal(BridgePort, r.Server!.BridgePort);
        Assert.Equal(game, r.Server.GameFolder);
        Assert.Equal("tok", r.Token);
        Assert.Equal("hello from the plugin", ReadThrough(r.LocalPort));
        // A port given wins over the folder's.
        r = await Tunnel.Start(Req(folder: game, bridge: 4321));
        Assert.Equal(4321, r.Server!.BridgePort);
        // No settings there: the usual port.
        File.Delete(file);
        r = await Tunnel.Start(Req(folder: game));
        Assert.Equal(5182, r.Server!.BridgePort);
        // A port no number of 1 to 65535 fits: the usual port too (it threw, leaving the connection open).
        File.WriteAllText(file, "Port = 99999999999\n");
        r = await Tunnel.Start(Req(folder: game));
        Assert.Equal(5182, r.Server!.BridgePort);
    }

    // A port out of range given: said before connecting (it threw).
    [Fact]
    public async Task APortOutOfRangeIsRefused()
    {
        Assert.Equal("A port is a number from 1 to 65535.", (await Tunnel.Start(new Tunnel.Request("127.0.0.1", 70000, "u", "pw", null, null, "tok", null, false, null))).Error);
        Assert.Equal("A port is a number from 1 to 65535.", (await Tunnel.Start(Req(bridge: 0))).Error);
        Assert.False(Tunnel.Open);
    }

    [Fact]
    public async Task EveryReasonThePluginDoesNotAnswerIsDiagnosed()
    {
        Ssh.Need();
        Assert.Null((await Tunnel.Start(Req(bridge: BridgePort))).Error);
        string game = Path.Combine(Dir, "valheim");
        Assert.Null(await Tunnel.Diagnose(" "));
        Assert.Equal($"The folder {game} does not exist on the server: check the server's Valheim folder.", await Tunnel.Diagnose(game + "/"));
        Directory.CreateDirectory(game);
        Assert.Equal($"BepInEx is not installed in {game} on the server: install BepInExPack for Valheim there.", await Tunnel.Diagnose(game));
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins", "Tie-WorldEditorBridge"));
        Assert.Equal($"The plugin is not in {game}/BepInEx/plugins on the server: copy WorldEditorBridge.dll there and restart the server.", await Tunnel.Diagnose(game));
        File.WriteAllText(Path.Combine(game, "BepInEx", "plugins", "Tie-WorldEditorBridge", "WorldEditorBridge.dll"), "");
        Assert.Equal("The plugin is installed but has never run: restart the server with BepInEx.", await Tunnel.Diagnose(game));
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "config"));
        File.WriteAllText(Path.Combine(game, "BepInEx", "config", LocalGame.OldConfigName), "");
        Assert.StartsWith("The plugin is installed but does not answer", await Tunnel.Diagnose(game));
        File.Move(Path.Combine(game, "BepInEx", "config", LocalGame.OldConfigName), Path.Combine(game, "BepInEx", "config", LocalGame.ConfigName));
        Assert.StartsWith("The plugin is installed but does not answer", await Tunnel.Diagnose(game));
    }

    // The check of the server's folder waits on the server (15 s at most): it runs off the caller's thread, so the
    // window does not freeze; it held the UI thread for the whole command.
    [Fact]
    public async Task TheServerCheckDoesNotHoldTheCallersThread()
    {
        Ssh.Need();
        // A relay in front of sshd that holds the server's answers while the test holds the gate: the check cannot
        // finish before the test lets it, however fast the server answers.
        using var relay = new TcpListener(IPAddress.Loopback, 0);
        relay.Start();
        var gate = new SemaphoreSlim(1, 1);
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = Task.Run(async () =>
        {
            using TcpClient app = await relay.AcceptTcpClientAsync(ct);
            using var server = new TcpClient();
            await server.ConnectAsync(IPAddress.Loopback, Ssh.Port, ct);
            NetworkStream toApp = app.GetStream();
            NetworkStream toServer = server.GetStream();
            Task up = toApp.CopyToAsync(toServer, ct);
            byte[] buffer = new byte[65536];
            int n;
            while ((n = await toServer.ReadAsync(buffer, ct)) > 0)
            {
                await gate.WaitAsync(ct);
                try
                {
                    await toApp.WriteAsync(buffer.AsMemory(0, n), ct);
                }
                finally
                {
                    gate.Release();
                }
            }
            await up;
        }, ct);
        Assert.Null((await Tunnel.Start(Req(bridge: BridgePort, sshPort: ((IPEndPoint)relay.LocalEndpoint).Port))).Error);
        await gate.WaitAsync(ct);
        var check = Tunnel.Diagnose(Path.Combine(Dir, "valheim"));
        Assert.False(check.IsCompleted);
        gate.Release();
        Assert.StartsWith("The folder ", await check);
    }

    // The stand-in plugin's wait for the next connection ending with an error (its listener stopped under it) fails
    // the disposal instead of passing unseen.
    [Fact]
    public void AnErrorInTheStandInPluginFailsTheDisposal()
    {
        var other = new TunnelPluginTests(Ssh);
        try
        {
            other.Bridge.Stop();
            Assert.True(SpinWait.SpinUntil(() => other.Hello.IsFaulted, TimeSpan.FromSeconds(30)), "the stand-in plugin did not fail");

            var e = Assert.Throws<AggregateException>(other.Dispose);

            Assert.NotNull(e.InnerException);
        }
        finally
        {
            // What the failed disposal left: its stop and its folder.
            other.Stop.Dispose();
            if (Directory.Exists(other.Dir))
            {
                Directory.Delete(other.Dir, true);
            }
        }
    }
}

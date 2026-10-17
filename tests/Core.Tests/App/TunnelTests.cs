using System.Net;
using System.Net.Sockets;
using System.Text;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The tunnel against a real SSH server (LocalSshd): logging in with a key, the forward to the plugin's port, trusting
/// the server's key on first use and refusing it once it changes, the saved server's token and port, the plugin's port
/// read from the server's game folder, and every diagnosis of a plugin that does not answer.
/// </summary>
[Collection("DataDir")]
public sealed class TunnelTests : IClassFixture<LocalSshd>, IDisposable
{
    private readonly LocalSshd _ssh;
    private readonly string? _servers = ServerConfig.PathOverride;
    private readonly string? _home = Environment.GetEnvironmentVariable("HOME");
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-tunnel-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly TcpListener _bridge = new(IPAddress.Loopback, 0);
    /// <summary>Ends the stand-in plugin's wait for the next connection (Dispose).</summary>
    private readonly CancellationTokenSource _stop = new();
    /// <summary>The stand-in plugin's work: Dispose waits for it to end, and its errors fail the test.</summary>
    private readonly Task _hello;

    public TunnelTests(LocalSshd ssh)
    {
        _ssh = ssh;
        Directory.CreateDirectory(_dir);
        ServerConfig.PathOverride = Path.Combine(_dir, "servers.cfg");
        // A home with no keys: the user's own ~/.ssh is never read.
        Directory.CreateDirectory(Path.Combine(_dir, "empty-home"));
        Environment.SetEnvironmentVariable("HOME", Path.Combine(_dir, "empty-home"));
        // The stand-in plugin: says hello to whoever comes through the tunnel.
        _bridge.Start();
        _hello = Task.Run(SayHello);
    }

    /// <summary>
    /// Says hello to each connection until Dispose stops it.
    /// </summary>
    private async Task SayHello()
    {
        while (true)
        {
            TcpClient c;
            try
            {
                c = await _bridge.AcceptTcpClientAsync(_stop.Token);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                return;
            }
            using (c)
            {
                await c.GetStream().WriteAsync(Encoding.ASCII.GetBytes("hello from the plugin\n"));
            }
        }
    }

    private int BridgePort => ((IPEndPoint)_bridge.LocalEndpoint).Port;

    public void Dispose()
    {
        Tunnel.Close();
        ServerConfig.PathOverride = _servers;
        Environment.SetEnvironmentVariable("HOME", _home);
        _stop.Cancel();
        Assert.True(_hello.Wait(TimeSpan.FromSeconds(30)), "the stand-in plugin did not stop");
        _stop.Dispose();
        _bridge.Stop();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>
    /// A tunnel request to the local sshd as the test's user, with its key unless another is given.
    /// </summary>
    private Tunnel.Request Req(string? key = null, string? token = "tok", int? bridge = null, string? password = null, bool savePassword = false, string? name = null, string? folder = null, int? sshPort = null) =>
        new("127.0.0.1", sshPort ?? _ssh.Port, _ssh.User, password, key ?? _ssh.Key, null, token, bridge, savePassword, name, folder);

    /// <summary>
    /// The first line a local port answers (10 s at most).
    /// </summary>
    private static string ReadThrough(int localPort)
    {
        using var c = new TcpClient();
        c.Connect(IPAddress.Loopback, localPort);
        c.ReceiveTimeout = 10000;
        return new StreamReader(c.GetStream()).ReadLine() ?? "";
    }

    [Fact]
    public async Task AKeyLogsInAndTheTunnelReachesThePlugin()
    {
        _ssh.Need();
        var r = await Tunnel.Start(Req(bridge: BridgePort, password: "unused", savePassword: false));
        Assert.Null(r.Error);
        Assert.True(Tunnel.Open);
        Assert.Equal(_ssh.Fingerprint, r.Fingerprint);
        Assert.Equal("tok", r.Token);
        Assert.Equal("hello from the plugin", ReadThrough(r.LocalPort));
        // What to remember: the server's key, the plugin's port and token, no password unless asked.
        var s = r.Server!;
        Assert.Equal("127.0.0.1", s.Name);
        Assert.Equal(("127.0.0.1", _ssh.Port, _ssh.User), (s.Host, s.SshPort, s.User));
        Assert.Equal(_ssh.Fingerprint, s.HostKey);
        Assert.Equal(BridgePort, s.BridgePort);
        Assert.Equal(_ssh.Key, s.KeyFile);
        Assert.Null(s.Password);
        Assert.Null(s.GameFolder);
        Tunnel.Close();
        Assert.False(Tunnel.Open);
        // Asked to: the password is kept, and the name given.
        r = await Tunnel.Start(Req(bridge: BridgePort, password: "pw", savePassword: true, name: " My server "));
        Assert.Null(r.Error);
        Assert.Equal("pw", r.Server!.Password);
        Assert.Equal("My server", r.Server.Name);
    }

    [Fact]
    public async Task ASecondTunnelReplacesTheFirst()
    {
        _ssh.Need();
        Assert.Null((await Tunnel.Start(Req(bridge: BridgePort))).Error);
        var second = await Tunnel.Start(Req(bridge: BridgePort));
        Assert.Null(second.Error);
        Assert.True(Tunnel.Open);
        Assert.Equal("hello from the plugin", ReadThrough(second.LocalPort));
    }

    [Fact]
    public async Task ACloseWhileConnectingLeavesNoTunnelOpen()
    {
        _ssh.Need();
        // Start waits on the SSH handshake; the editor quits meanwhile.
        var start = Tunnel.Start(Req(bridge: BridgePort));
        Tunnel.Close();
        var r = await start;
        Assert.Equal("The connection was closed while it was being made.", r.Error);
        Assert.Equal(0, r.LocalPort);
        Assert.False(Tunnel.Open);
        // A later connection is kept.
        Assert.Null((await Tunnel.Start(Req(bridge: BridgePort))).Error);
        Assert.True(Tunnel.Open);
    }

    [Fact]
    public async Task AKeyTheServerDoesNotKnowOrAPasswordItDoesNotTakeIsRefused()
    {
        _ssh.Need();
        var r = await Tunnel.Start(Req(key: _ssh.OtherKey));
        Assert.Equal("The server refused the login: check the user name and the password or key.", r.Error);
        Assert.False(Tunnel.Open);
        // This server takes keys only.
        r = await Tunnel.Start(Req(key: "", password: "pw"));
        Assert.Equal("The server refused the login: check the user name and the password or key.", r.Error);
    }

    [Fact]
    public async Task AKeyInAFormatSshNetRefusesIsSaidAndSkippedInTheHome()
    {
        // Each refused in its own way: DSA (not supported), PKCS#8 with no key in it, an OpenSSH key cut short.
        string[] refused =
        [
            "-----BEGIN DSA PRIVATE KEY-----\nAAAA\n-----END DSA PRIVATE KEY-----\n",
            "-----BEGIN PRIVATE KEY-----\nAAAA\n-----END PRIVATE KEY-----\n",
            "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAA\n-----END OPENSSH PRIVATE KEY-----\n",
            "-----BEGIN RSA PRIVATE KEY-----\nAAAA\n-----END RSA PRIVATE KEY-----\n",
        ];
        string key = Path.Combine(_dir, "key");
        string home = Path.Combine(_dir, "empty-home", ".ssh");
        Directory.CreateDirectory(home);
        foreach (string text in refused)
        {
            File.WriteAllText(key, text);
            Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req(key: key))).Error);
            // One of the usual keys: skipped, so only a password is missing.
            File.WriteAllText(Path.Combine(home, "id_ecdsa"), text);
            Assert.StartsWith("Enter the password, or choose your SSH key file", (await Tunnel.Start(Req(key: ""))).Error);
        }
        Assert.False(Tunnel.Open);
    }

    [Fact]
    public async Task TheUsualKeysOfTheHomeAndATildePathAreUsed()
    {
        _ssh.Need();
        string home = Path.Combine(_dir, "home");
        Directory.CreateDirectory(Path.Combine(home, ".ssh"));
        File.Copy(_ssh.Key, Path.Combine(home, ".ssh", "id_ed25519"));
        // One the editor cannot read is skipped (it stopped the login with "The key file could not be read").
        File.WriteAllText(Path.Combine(home, ".ssh", "id_ecdsa"), "not a key");
        File.Copy(_ssh.Key, Path.Combine(home, "mykey"));
        Environment.SetEnvironmentVariable("HOME", home);
        var r = await Tunnel.Start(Req(key: ""));
        Assert.Null(r.Error);
        Assert.Null(r.Server!.KeyFile);
        r = await Tunnel.Start(Req(key: "~/mykey"));
        Assert.Null(r.Error);
        Assert.Equal("~/mykey", r.Server!.KeyFile);
    }

    [Fact]
    public async Task TheServersKeyIsTrustedOnceAndRefusedWhenItChanges()
    {
        _ssh.Need();
        string id = $"{_ssh.User}@127.0.0.1:{_ssh.Port}";
        ServerConfig.Remember(new ServerConfig.Server { Name = "Saved", Host = "127.0.0.1", SshPort = _ssh.Port, User = _ssh.User, Token = "saved-token", BridgePort = BridgePort, HostKey = _ssh.Fingerprint, GameFolder = "/nowhere" });
        Assert.Contains(ServerConfig.Load(), s => s.Id == id);
        // The same key: the saved token, port, name and folder fill what the request leaves out.
        var r = await Tunnel.Start(Req(token: " ", bridge: null));
        Assert.Null(r.Error);
        Assert.Equal("saved-token", r.Token);
        Assert.Equal(BridgePort, r.Server!.BridgePort);
        Assert.Equal("Saved", r.Server.Name);
        Assert.Equal("/nowhere", r.Server.GameFolder);
        Assert.Equal("hello from the plugin", ReadThrough(r.LocalPort));
        // Another key than the one remembered: refused, saying both, and naming every entry that pins the old key.
        ServerConfig.Remember(new ServerConfig.Server { Name = "Other account", Host = "127.0.0.1", SshPort = _ssh.Port, User = "someone-else", HostKey = "SHA256:somethingelse" });
        ServerConfig.Remember(new ServerConfig.Server { Name = "Saved", Host = "127.0.0.1", SshPort = _ssh.Port, User = _ssh.User, HostKey = "SHA256:somethingelse" });
        r = await Tunnel.Start(Req());
        Assert.StartsWith($"The server's identity changed (it was SHA256:somethingelse, now {_ssh.Fingerprint}).", r.Error);
        Assert.Contains("empty the HostKey line of [Saved], [Other account] in", r.Error);
        Assert.False(Tunnel.Open);
        // Known under another account only: the same server, so still refused (it was trusted afresh).
        ServerConfig.Forget(id);
        r = await Tunnel.Start(Req());
        Assert.StartsWith("The server's identity changed (it was SHA256:somethingelse", r.Error);
        Assert.Contains("empty the HostKey line of [Other account] in", r.Error);
    }

    [Fact]
    public async Task ALineBreakInWhatIsSavedIsRefusedBeforeConnecting()
    {
        var r = await Tunnel.Start(Req(token: "tok\nHostKey = SHA256:other"));

        Assert.Equal("The server's details cannot hold line breaks: enter each on one line.", r.Error);
        Assert.False(Tunnel.Open);
    }

    [Fact]
    public async Task WithoutATokenTheTunnelIsNotOpened()
    {
        _ssh.Need();
        var r = await Tunnel.Start(Req(token: null));
        Assert.StartsWith("Enter the plugin's token", r.Error);
        Assert.False(Tunnel.Open);
    }

    [Theory]
    [InlineData(LocalGame.ConfigName, false)]
    [InlineData(LocalGame.OldConfigName, true)]
    public async Task ThePluginsPortIsReadFromTheServersGameFolder(string config, bool slash)
    {
        _ssh.Need();
        string game = Path.Combine(_dir, "it's a server");
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
        _ssh.Need();
        Assert.Null((await Tunnel.Start(Req(bridge: BridgePort))).Error);
        string game = Path.Combine(_dir, "valheim");
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
        _ssh.Need();
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
            await server.ConnectAsync(IPAddress.Loopback, _ssh.Port, ct);
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
        var check = Tunnel.Diagnose(Path.Combine(_dir, "valheim"));
        Assert.False(check.IsCompleted);
        gate.Release();
        Assert.StartsWith("The folder ", await check);
    }

    // The stand-in plugin's wait for the next connection ending with an error (its listener stopped under it) fails
    // the disposal instead of passing unseen.
    [Fact]
    public void AnErrorInTheStandInPluginFailsTheDisposal()
    {
        var other = new TunnelTests(_ssh);
        try
        {
            other._bridge.Stop();
            Assert.True(SpinWait.SpinUntil(() => other._hello.IsFaulted, TimeSpan.FromSeconds(30)), "the stand-in plugin did not fail");

            var e = Assert.Throws<AggregateException>(other.Dispose);

            Assert.NotNull(e.InnerException);
        }
        finally
        {
            // What the failed disposal left: its stop and its folder.
            other._stop.Dispose();
            if (Directory.Exists(other._dir))
            {
                Directory.Delete(other._dir, true);
            }
        }
    }
}

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
        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    using var c = await _bridge.AcceptTcpClientAsync();
                    await c.GetStream().WriteAsync(Encoding.ASCII.GetBytes("hello from the plugin\n"));
                }
            }
            catch
            {
                // The accept loop ends when the test disposes the listener; the test has finished by then.
            }
        });
    }

    private int BridgePort => ((IPEndPoint)_bridge.LocalEndpoint).Port;

    public void Dispose()
    {
        Tunnel.Close();
        _bridge.Stop();
        ServerConfig.PathOverride = _servers;
        Environment.SetEnvironmentVariable("HOME", _home);
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.
        }
    }

    /// <summary>
    /// A tunnel request to the local sshd as the test's user, with its key unless another is given.
    /// </summary>
    private Tunnel.Request Req(string? key = null, string? token = "tok", int? bridge = null, string? password = null, bool savePassword = false, string? name = null, string? folder = null) =>
        new("127.0.0.1", _ssh.Port, _ssh.User, password, key ?? _ssh.Key, null, token, bridge, savePassword, name, folder);

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
        // Another key than the one remembered: refused, saying both.
        ServerConfig.Remember(new ServerConfig.Server { Name = "Saved", Host = "127.0.0.1", SshPort = _ssh.Port, User = _ssh.User, HostKey = "SHA256:somethingelse" });
        r = await Tunnel.Start(Req());
        Assert.StartsWith($"The server's identity changed (it was SHA256:somethingelse, now {_ssh.Fingerprint}).", r.Error);
        Assert.False(Tunnel.Open);
        // Known under another account only: the same server, so still refused (it was trusted afresh).
        ServerConfig.Forget(id);
        ServerConfig.Remember(new ServerConfig.Server { Name = "Other account", Host = "127.0.0.1", SshPort = _ssh.Port, User = "someone-else", HostKey = "SHA256:somethingelse" });
        r = await Tunnel.Start(Req());
        Assert.StartsWith("The server's identity changed (it was SHA256:somethingelse", r.Error);
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
        File.WriteAllText(Path.Combine(game, "BepInEx", "config", config), $"[Bridge]\nToken = never read\n Port = {BridgePort}\n");
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
        File.Delete(Path.Combine(game, "BepInEx", "config", config));
        r = await Tunnel.Start(Req(folder: game));
        Assert.Equal(5182, r.Server!.BridgePort);
    }

    [Fact]
    public async Task EveryReasonThePluginDoesNotAnswerIsDiagnosed()
    {
        _ssh.Need();
        Assert.Null((await Tunnel.Start(Req(bridge: BridgePort))).Error);
        string game = Path.Combine(_dir, "valheim");
        Assert.Null(Tunnel.Diagnose(" "));
        Assert.Equal($"The folder {game} does not exist on the server: check the server's Valheim folder.", Tunnel.Diagnose(game + "/"));
        Directory.CreateDirectory(game);
        Assert.Equal($"BepInEx is not installed in {game} on the server: install BepInExPack for Valheim there.", Tunnel.Diagnose(game));
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins", "Tie-WorldEditorBridge"));
        Assert.Equal($"The plugin is not in {game}/BepInEx/plugins on the server: copy WorldEditorBridge.dll there and restart the server.", Tunnel.Diagnose(game));
        File.WriteAllText(Path.Combine(game, "BepInEx", "plugins", "Tie-WorldEditorBridge", "WorldEditorBridge.dll"), "");
        Assert.Equal("The plugin is installed but has never run: restart the server with BepInEx.", Tunnel.Diagnose(game));
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "config"));
        File.WriteAllText(Path.Combine(game, "BepInEx", "config", LocalGame.OldConfigName), "");
        Assert.StartsWith("The plugin is installed but does not answer", Tunnel.Diagnose(game));
        File.Move(Path.Combine(game, "BepInEx", "config", LocalGame.OldConfigName), Path.Combine(game, "BepInEx", "config", LocalGame.ConfigName));
        Assert.StartsWith("The plugin is installed but does not answer", Tunnel.Diagnose(game));
    }
}

using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The tunnel logging in to a real SSH server (LocalSshd): a key, the server's key trusted on first use and refused once it
/// changes, the saved server's token and port, and the requests refused before connecting.
/// </summary>
[Collection("DataDir")]
public sealed class TunnelLoginTests : TunnelTestBase, IClassFixture<LocalSshd>
{
    public TunnelLoginTests(LocalSshd ssh) : base(ssh)
    {
    }

    [Fact]
    public async Task AKeyLogsInAndTheTunnelReachesThePlugin()
    {
        Ssh.Need();
        var r = await Tunnel.Start(Req(bridge: BridgePort, password: "unused", savePassword: false));
        Assert.Null(r.Error);
        Assert.True(Tunnel.Open);
        Assert.Equal(Ssh.Fingerprint, r.Fingerprint);
        Assert.Equal("tok", r.Token);
        Assert.Equal("hello from the plugin", ReadThrough(r.LocalPort));
        // What to remember: the server's key, the plugin's port and token, no password unless asked.
        var s = r.Server!;
        Assert.Equal("127.0.0.1", s.Name);
        Assert.Equal(("127.0.0.1", Ssh.Port, Ssh.User), (s.Host, s.SshPort, s.User));
        Assert.Equal(Ssh.Fingerprint, s.HostKey);
        Assert.Equal(BridgePort, s.BridgePort);
        Assert.Equal(Ssh.Key, s.KeyFile);
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
        Ssh.Need();
        Assert.Null((await Tunnel.Start(Req(bridge: BridgePort))).Error);
        var second = await Tunnel.Start(Req(bridge: BridgePort));
        Assert.Null(second.Error);
        Assert.True(Tunnel.Open);
        Assert.Equal("hello from the plugin", ReadThrough(second.LocalPort));
    }

    [Fact]
    public async Task ACloseWhileConnectingLeavesNoTunnelOpen()
    {
        Ssh.Need();
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
        Ssh.Need();
        var r = await Tunnel.Start(Req(key: Ssh.OtherKey));
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
        string key = Path.Combine(Dir, "key");
        string home = Path.Combine(Dir, "empty-home", ".ssh");
        Directory.CreateDirectory(home);
        foreach (string text in refused)
        {
            File.WriteAllText(key, text);
            Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req(key: key))).Error);
            // One of the usual keys: skipped, so only a password is missing. (On Windows the usual keys are the real
            // profile's, which HOME does not point elsewhere: what is missing depends on the computer.)
            if (!OperatingSystem.IsWindows())
            {
                File.WriteAllText(Path.Combine(home, "id_ecdsa"), text);
                Assert.StartsWith("Enter the password, or choose your SSH key file", (await Tunnel.Start(Req(key: ""))).Error);
            }
        }
        Assert.False(Tunnel.Open);
    }

    [Fact]
    public async Task TheUsualKeysOfTheHomeAndATildePathAreUsed()
    {
        Ssh.Need();
        string home = Path.Combine(Dir, "home");
        Directory.CreateDirectory(Path.Combine(home, ".ssh"));
        File.Copy(Ssh.Key, Path.Combine(home, ".ssh", "id_ed25519"));
        // One the editor cannot read is skipped (it stopped the login with "The key file could not be read").
        File.WriteAllText(Path.Combine(home, ".ssh", "id_ecdsa"), "not a key");
        File.Copy(Ssh.Key, Path.Combine(home, "mykey"));
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
        Ssh.Need();
        string id = $"{Ssh.User}@127.0.0.1:{Ssh.Port}";
        ServerConfig.Remember(new ServerConfig.Server { Name = "Saved", Host = "127.0.0.1", SshPort = Ssh.Port, User = Ssh.User, Token = "saved-token", BridgePort = BridgePort, HostKey = Ssh.Fingerprint, GameFolder = "/nowhere" });
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
        ServerConfig.Remember(new ServerConfig.Server { Name = "Other account", Host = "127.0.0.1", SshPort = Ssh.Port, User = "someone-else", HostKey = "SHA256:somethingelse" });
        ServerConfig.Remember(new ServerConfig.Server { Name = "Saved", Host = "127.0.0.1", SshPort = Ssh.Port, User = Ssh.User, HostKey = "SHA256:somethingelse" });
        r = await Tunnel.Start(Req());
        Assert.StartsWith($"The server's identity changed (it was SHA256:somethingelse, now {Ssh.Fingerprint}).", r.Error);
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
        Ssh.Need();
        var r = await Tunnel.Start(Req(token: null));
        Assert.StartsWith("Enter the plugin's token", r.Error);
        Assert.False(Tunnel.Open);
    }
}

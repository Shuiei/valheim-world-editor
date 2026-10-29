using System.Net;
using System.Net.Sockets;
using System.Text;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The SSH tunnel's checks before any SSH session: user and address, the password or key, closed ports, a server that does not speak SSH.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderTunnelTests : HomeFolderTestBase
{
    /// <summary>
    /// A tunnel request with token "token" and nothing else set beyond what is given.
    /// </summary>
    private static Tunnel.Request Req(string host, int port, string user = "valheim", string? password = null, string? key = null, string? phrase = null) =>
        new(host, port, user, password, key, phrase, "token", null, false, null);
    [Fact]
    public async Task TheTunnelNeedsAUserAndAnAddress()
    {
        Assert.StartsWith("Enter the server as user@address", (await Tunnel.Start(Req("", 22))).Error);
        Assert.StartsWith("Enter the server as user@address", (await Tunnel.Start(Req("server.example", 22, user: " "))).Error);
        Assert.StartsWith("Enter the server as user@address", (await Tunnel.Start(Req("@server.example", 22))).Error);
        Assert.False(Tunnel.Open);
    }

    [Fact]
    public async Task TheTunnelNeedsAPasswordOrAKeyItCanRead()
    {
        // No key given and none in this home's .ssh: a password or a key is needed. (On Windows the usual keys are the
        // real profile's, which the pretend home cannot point elsewhere: what is missing depends on the computer.)
        if (!OperatingSystem.IsWindows())
        {
            Assert.StartsWith("Enter the password, or choose your SSH key file", (await Tunnel.Start(Req("server.example", 22))).Error);
        }
        // Keys that cannot be read: missing (also through ~), not a key, locked with another passphrase.
        Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req("server.example", 22, key: Local("missing")))).Error);
        Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req("server.example", 22, key: "~/missing"))).Error);
        File.WriteAllText(Local("notakey"), "hello");
        Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req("server.example", 22, key: $"\"{Local("notakey")}\""))).Error);
        File.WriteAllText(Local("locked"), LockedKey);
        Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req("server.example", 22, key: Local("locked"), phrase: "wrong"))).Error);
    }

    [Fact]
    public async Task ATunnelToAClosedPortSaysNothingAnswers()
    {
        File.WriteAllText(Local("key"), TestKey);
        int port = FakeGame.FreePort();
        var r = await Tunnel.Start(Req("valheim@127.0.0.1", port, user: "ignored", key: Local("key")));
        Assert.Equal($"No answer from 127.0.0.1:{port}. Check the address and that SSH is enabled on the server.", r.Error);
        Assert.Equal(0, r.LocalPort);
        // The usual key files of this home are used when no key is given (here the test key).
        Directory.CreateDirectory(Local(".ssh"));
        File.WriteAllText(Local(".ssh", "id_ed25519"), TestKey);
        File.WriteAllText(Local(".ssh", "id_rsa"), LockedKey);
        Assert.StartsWith("No answer from", (await Tunnel.Start(Req("127.0.0.1", port))).Error);
        // A password alone is enough to try (always a closed port here: never a real SSH server).
        File.Delete(Local(".ssh", "id_ed25519"));
        File.Delete(Local(".ssh", "id_rsa"));
        Assert.StartsWith("No answer from", (await Tunnel.Start(Req("127.0.0.1", port, password: "pw"))).Error);
        // The locked key opens with its passphrase.
        File.WriteAllText(Local("locked"), LockedKey);
        Assert.StartsWith("No answer from", (await Tunnel.Start(Req("127.0.0.1", port, key: Local("locked"), phrase: "secret"))).Error);
    }

    // A key ssh-keygen cannot make (its folder is missing) fails the set-up with ssh-keygen's exit code.
    [Fact]
    public void AKeyThatCannotBeMadeSaysSshKeygensExitCode()
    {
        var e = Assert.Throws<InvalidOperationException>(() => MakeKey(Local("missing", "key"), ""));
        Assert.StartsWith("ssh-keygen failed with exit code ", e.Message);
    }

    [Fact]
    public async Task AServerThatDoesNotSpeakSshIsReported()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync();
            var s = c.GetStream();
            await s.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));
            await Task.Delay(200);
        }, TestContext.Current.CancellationToken);
        try
        {
            File.WriteAllText(Local("key"), TestKey);
            var r = await Tunnel.Start(Req("127.0.0.1", port, key: Local("key")));
            Assert.NotNull(r.Error);
            Assert.True(r.Error!.StartsWith("Could not connect", StringComparison.Ordinal) || r.Error.StartsWith("No answer from", StringComparison.Ordinal), r.Error);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task WithoutATunnelThereIsNothingToDiagnoseOrClose()
    {
        Tunnel.Close();
        Tunnel.Close();
        Assert.False(Tunnel.Open);
        Assert.Null(await Tunnel.Diagnose("/home/valheim/server"));
        Assert.Null(await Tunnel.Diagnose(null));
    }
}

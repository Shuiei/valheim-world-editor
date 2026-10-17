using System.Net;
using System.Net.Sockets;
using System.Text;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The set-up the tunnel tests share (TunnelLoginTests, TunnelPluginTests): a home with no keys, a server settings file of
/// its own, and a stand-in plugin (a listener that says hello to each connection) behind the tunnel.
/// </summary>
public abstract class TunnelTestBase : IDisposable
{
    /// <summary>The local sshd the tests log in to.</summary>
    protected LocalSshd Ssh { get; }
    private readonly string? _servers = ServerConfig.PathOverride;
    private readonly string? Home = Environment.GetEnvironmentVariable("HOME");
    /// <summary>The test's own folder (its home, its server settings), deleted after.</summary>
    protected string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-tunnel-" + Guid.NewGuid().ToString("N")[..8]);
    /// <summary>The stand-in plugin's listener, behind the tunnel.</summary>
    protected TcpListener Bridge { get; } = new(IPAddress.Loopback, 0);
    /// <summary>Ends the stand-in plugin's wait for the next connection (Dispose).</summary>
    protected CancellationTokenSource Stop { get; } = new();
    /// <summary>The stand-in plugin's work: Dispose waits for it to end, and its errors fail the test.</summary>
    protected Task Hello { get; }

    /// <summary>
    /// Points the tunnel's files and home at this test's own folder, and starts the stand-in plugin.
    /// </summary>
    protected TunnelTestBase(LocalSshd ssh)
    {
        Ssh = ssh;
        Directory.CreateDirectory(Dir);
        ServerConfig.PathOverride = Path.Combine(Dir, "servers.cfg");
        // A home with no keys: the user's own ~/.ssh is never read.
        Directory.CreateDirectory(Path.Combine(Dir, "empty-home"));
        Environment.SetEnvironmentVariable("HOME", Path.Combine(Dir, "empty-home"));
        // The stand-in plugin: says hello to whoever comes through the tunnel.
        Bridge.Start();
        Hello = Task.Run(SayHello);
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
                c = await Bridge.AcceptTcpClientAsync(Stop.Token);
            }
            catch (OperationCanceledException) when (Stop.IsCancellationRequested)
            {
                return;
            }
            using (c)
            {
                await c.GetStream().WriteAsync(Encoding.ASCII.GetBytes("hello from the plugin\n"));
            }
        }
    }

    /// <summary>The stand-in plugin's port.</summary>
    protected int BridgePort => ((IPEndPoint)Bridge.LocalEndpoint).Port;

    public void Dispose()
    {
        Tunnel.Close();
        ServerConfig.PathOverride = _servers;
        Environment.SetEnvironmentVariable("HOME", Home);
        Stop.Cancel();
        Assert.True(Hello.Wait(TimeSpan.FromSeconds(30)), "the stand-in plugin did not stop");
        Stop.Dispose();
        Bridge.Stop();
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A tunnel request to the local sshd as the test's user, with its key unless another is given.
    /// </summary>
    protected Tunnel.Request Req(string? key = null, string? token = "tok", int? bridge = null, string? password = null, bool savePassword = false, string? name = null, string? folder = null, int? sshPort = null) =>
        new("127.0.0.1", sshPort ?? Ssh.Port, Ssh.User, password, key ?? Ssh.Key, null, token, bridge, savePassword, name, folder);

    /// <summary>
    /// The first line a local port answers (10 s at most).
    /// </summary>
    protected static string ReadThrough(int localPort)
    {
        using var c = new TcpClient();
        c.Connect(IPAddress.Loopback, localPort);
        c.ReceiveTimeout = 10000;
        return new StreamReader(c.GetStream()).ReadLine() ?? "";
    }
}

using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The saved servers file (ServerConfig): ports out of range read as the defaults, and no value can add a line of its
/// own (a name's line break becomes a space; any other value with one is refused).
/// </summary>
[Collection("DataDir")]
public sealed class ServerConfigTests : IDisposable
{
    private readonly string? _old = ServerConfig.PathOverride;
    private readonly string _file = Path.Combine(Path.GetTempPath(), "vwe-servers-" + Guid.NewGuid().ToString("N")[..8] + ".cfg");

    public ServerConfigTests()
    {
        ServerConfig.PathOverride = _file;
    }

    public void Dispose()
    {
        ServerConfig.PathOverride = _old;
        File.Delete(_file);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    public void APortOutOfRangeReadsAsTheDefault(string port)
    {
        File.WriteAllText(_file, $"[S]\nHost = h\nUser = u\nSshPort = {port}\nBridgePort = {port}\n");

        ServerConfig.Server s = Assert.Single(ServerConfig.Load());

        Assert.Equal((22, 5182), (s.SshPort, s.BridgePort));
    }

    [Fact]
    public void ALineBreakNeverAddsALineOfItsOwn()
    {
        ServerConfig.Remember(new ServerConfig.Server { Name = "Two\nlines", Host = "h", User = "u", HostKey = "SHA256:kept" });

        Assert.Throws<ArgumentException>(() => ServerConfig.Remember(new ServerConfig.Server { Host = "h", User = "u", Token = "t\nHostKey = SHA256:other" }));

        ServerConfig.Server s = Assert.Single(ServerConfig.Load());
        Assert.Equal(("Two lines", "SHA256:kept", null), (s.Name, s.HostKey, s.Token));
        Assert.True(ServerConfig.OneLine(null));
        Assert.False(ServerConfig.OneLine("a\rb"));
    }
}

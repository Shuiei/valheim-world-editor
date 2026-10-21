using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What each key of a servers.cfg section sets (ServerConfigKeys.Apply).
/// </summary>
public class ServerConfigKeysTests
{
    [Fact]
    public void KeysAreMatchedWithoutCase()
    {
        var s = new ServerConfig.Server();
        ServerConfigKeys.Apply(s, "HOST", "h");
        Assert.Equal("h", s.Host);
    }

    [Fact]
    public void AnEmptyValueClearsAnOptionalField()
    {
        var s = new ServerConfig.Server { Token = "x" };
        ServerConfigKeys.Apply(s, "token", "");
        Assert.Null(s.Token);
    }

    [Fact]
    public void AnInvalidSshPortFallsBackToTwentyTwo()
    {
        var s = new ServerConfig.Server { SshPort = 2222 };
        ServerConfigKeys.Apply(s, "sshport", "abc");
        Assert.Equal(22, s.SshPort);
    }

    [Fact]
    public void AnUnknownKeyChangesNothing()
    {
        var s = new ServerConfig.Server { Host = "h" };
        ServerConfigKeys.Apply(s, "nope", "v");
        Assert.Equal("h", s.Host);
    }
}

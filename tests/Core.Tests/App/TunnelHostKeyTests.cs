using TerrainEditor.App;
using Renci.SshNet.Common;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The host key of a tunnel's server (TunnelHostKey): the messages for a failed connection, and the saved entries that
/// count as the same server.
/// </summary>
public class TunnelHostKeyTests
{
    [Fact]
    public void ALoginRefusalIsSaidAsSuch()
    {
        string message = new TunnelHostKey(null).ConnectFailure(new SshAuthenticationException("denied"), "h", 22, []);
        Assert.Contains("refused the login", message);
    }

    [Fact]
    public void ATimeoutNamesTheAddressThatDidNotAnswer()
    {
        string message = new TunnelHostKey(null).ConnectFailure(new SshOperationTimeoutException("slow"), "h", 2222, []);
        Assert.Contains("No answer from h:2222", message);
    }

    [Fact]
    public void OtherFailuresCarryTheirMessage()
    {
        string message = new TunnelHostKey(null).ConnectFailure(new InvalidOperationException("boom"), "h", 22, []);
        Assert.Equal("Could not connect: boom", message);
    }

    [Fact]
    public void SameServerEntriesAreTheOnesWithTheSameAddressAndPortAndAHostKey()
    {
        var keyed = new ServerConfig.Server { Name = "A", Host = "H", SshPort = 22, User = "u", HostKey = "k" };
        var noKey = new ServerConfig.Server { Name = "B", Host = "h", SshPort = 22, User = "u" };
        var otherPort = new ServerConfig.Server { Name = "C", Host = "h", SshPort = 2222, User = "u", HostKey = "k" };
        var otherHost = new ServerConfig.Server { Name = "D", Host = "other", SshPort = 22, User = "u", HostKey = "k" };
        var same = TunnelHostKey.SameServerEntries([keyed, noKey, otherPort, otherHost], "h", 22);
        Assert.Equal([keyed], same);
    }
}

using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The entry a tunnel is made from (TunnelTarget): "user@host" is split, and an entry is refused when its address or
/// account is missing, a port is out of range, or a field holds a line break.
/// </summary>
public class TunnelTargetTests
{
    private static Tunnel.Request Entry(string host, string user = "root", string? password = null, int? bridgePort = null) =>
        new(host, 22, user, password, null, null, "token", bridgePort, false, null, null);

    [Fact]
    public void AnAtSignInTheHostTakesTheUserFromIt()
    {
        var (host, user) = TunnelTarget.Split(Entry("root@example.com", user: "ignored"));
        Assert.Equal(("example.com", "root"), (host, user));
    }

    [Fact]
    public void AHostWithoutAnAtSignKeepsTheUserField()
    {
        var (host, user) = TunnelTarget.Split(Entry("example.com", user: "me"));
        Assert.Equal(("example.com", "me"), (host, user));
    }

    [Fact]
    public void TheSplitTrimsBothFields()
    {
        var (host, user) = TunnelTarget.Split(Entry("  example.com  ", user: " me "));
        Assert.Equal(("example.com", "me"), (host, user));
    }

    [Fact]
    public void AMissingAddressIsRefusedWithTheFormatToEnter()
    {
        string? problem = TunnelTarget.Problem(Entry(""), "", "root", 22);
        Assert.Contains("user@address", problem);
    }

    [Fact]
    public void APortOutOfRangeIsRefused()
    {
        string? problem = TunnelTarget.Problem(Entry("h"), "h", "root", 70000);
        Assert.Contains("1 to 65535", problem);
    }

    [Fact]
    public void ABridgePortOutOfRangeIsRefused()
    {
        string? problem = TunnelTarget.Problem(Entry("h", bridgePort: 0), "h", "root", 22);
        Assert.Contains("1 to 65535", problem);
    }

    [Fact]
    public void ALineBreakInAFieldIsRefused()
    {
        string? problem = TunnelTarget.Problem(Entry("h", password: "a\nb"), "h", "root", 22);
        Assert.Contains("line breaks", problem);
    }

    [Fact]
    public void AUsableEntryHasNoProblem()
    {
        Assert.Null(TunnelTarget.Problem(Entry("h"), "h", "root", 22));
    }
}

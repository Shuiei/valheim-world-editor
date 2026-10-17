using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Quoting a path for the server's shell (TunnelServerCheck.Quote).
/// </summary>
public class TunnelServerCheckTests
{
    [Fact]
    public void APathIsWrappedInSingleQuotes()
    {
        Assert.Equal("'/srv/valheim'", TunnelServerCheck.Quote("/srv/valheim"));
    }

    [Fact]
    public void AnEmbeddedSingleQuoteIsEscapedForTheShell()
    {
        Assert.Equal("'a'\\''b'", TunnelServerCheck.Quote("a'b"));
    }
}

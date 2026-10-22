using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.StartPageRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// I made my own tunnel: the address typed names the live world "live &lt;address&gt;", the name its kept history is found
/// by (the map's header shows the address alone: MapPageLifecycleTests).
/// </summary>
[Collection("DataDir")]
public class StartPageConnectUrlTests
{
    [AvaloniaFact]
    public async Task TheAddressTypedNamesTheLiveWorld()
    {
        using var game = new FakeGame();
        using var r = new Run();
        r.P.LiveUrl.Text = $" 127.0.0.1:{game.Port} ";
        r.P.LiveToken.Text = game.Token;
        await r.P.ConnectUrl();
        using var world = await r.Open!();
        Assert.True(world.IsLive);
        Assert.Equal("live " + game.Url, world.Label);
    }
}

using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.WorldSessionRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Opening a live world (WorldSession.OpenLive) on a game whose mods cannot be learnt: a bridge whose catalogue fails,
/// or that refuses its list of mods, still gives a session, with no mod prefabs known from it.
/// </summary>
[Collection("World files")]
public sealed class WorldSessionOpenLiveTests
{
    // The plugin's catalogue fails (a game still loading, an error in a mod): the world opens without the mods' prefabs.
    [Fact]
    public async Task AGameWhoseCatalogueFailsStillOpens()
    {
        using var game = new FakeGame();
        game.Fail["/catalogue"] = 500;
        using var world = await OpenLive(game);
        Assert.True(world.IsLive);
        Assert.True(world.World.Objects.Count > 0);
    }

    // The bridge refuses its list of mods (an error, not an older plugin's 404): the world opens all the same.
    [Fact]
    public async Task ABridgeRefusingItsModsListStillOpens()
    {
        using var game = new FakeGame();
        game.Fail["/mods"] = 500;
        using var world = await OpenLive(game);
        Assert.True(world.IsLive);
    }
}

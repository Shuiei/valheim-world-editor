using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.WorldSessionRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Opening a live world (WorldSession.OpenLive) with the Expand World switch on: the world's rules come from the mods'
/// files the game's bridge serves, in a data folder of the test's own.
/// </summary>
[Collection("DataDir")]
public sealed class ExpandWorldLiveDesktopTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-liveew-" + Guid.NewGuid().ToString("N")[..8]);

    public ExpandWorldLiveDesktopTests()
    {
        AppSettings.DataDirOverride = _dir;
        GameData.UseCopies(Fixtures.Root);
    }

    /// <summary>Puts the data folder and the tests' copies back, and deletes the folder.</summary>
    public void Dispose()
    {
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public async Task ALiveWorldFollowsTheModsTheGameRuns()
    {
        var settings = AppSettings.Load();
        settings.ExpandWorld = true;
        Assert.Null(settings.Save());
        using var game = new FakeGame { Plugins = [("expand_world_size", "Expand World Size", "1.45.0")] };
        game.AddConfig("expand_world_size.cfg", "[1. General]\nWorld radius = 20000\n");
        using var world = await OpenLive(game);
        Assert.True(world.IsLive);
        Assert.Equal(20000f, world.World.Rules.Radius);
        Assert.True(world.World.Rules.SizeInstalled);
        Assert.True(File.Exists(Path.Combine(ExpandWorld.LiveFolder, "config", "expand_world_size.cfg")));
    }
}

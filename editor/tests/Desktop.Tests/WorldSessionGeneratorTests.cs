using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A world made with a world generation mod as it opens (WorldSession.TakeGenerator): its rules take the mod's
/// generator, told the version a live game runs, and a world it cannot draw is refused with its words; the version a
/// live game runs, asked of its bridge. With a made-up mod. Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class WorldSessionGeneratorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-worldgen-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FlatlandGenerator _flatland = new();

    public WorldSessionGeneratorTests() => ModExtensions.UseForTests(("Flatland", _flatland));

    public void Dispose()
    {
        ModExtensions.Forget();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>A world Flatland made at this height, in a folder of its own, as read from it.</summary>
    private WorldSave World(float height)
    {
        var g = WorldGenerators.Find("test.flatland")!;
        string folder = Path.Combine(_dir, Guid.NewGuid().ToString("N")[..8], "Flat");
        WorldCreator.Create(folder, "Flat", "abcde", rules: new WorldRules { Generator = g.Rules(FlatlandGenerator.Settings(height), null) });
        return WorldSave.Load(folder);
    }

    [Fact]
    public void ItsRulesTakeTheGeneratorToldTheGamesVersion()
    {
        var world = World(50f);
        WorldSession.TakeGenerator(world, "2.0.1");
        Assert.Equal(("Flatland", "2.0.0", "height 50"), (world.Rules.Generator?.Name, world.Rules.Generator?.Version, world.Rules.Generator?.Settings));
        Assert.Equal("2.0.1", _flatland.GameVersion);
    }

    [Fact]
    public void AWorldItCannotDrawIsRefusedInItsWords()
    {
        Assert.Equal(FlatlandGenerator.TooHigh, Assert.Throws<InvalidOperationException>(() => WorldSession.TakeGenerator(World(2000f))).Message);
        Assert.Equal("The game runs Flatland 0.9.0.", Assert.Throws<InvalidOperationException>(() => WorldSession.TakeGenerator(World(50f), "0.9.0")).Message);
    }

    [Fact]
    public async Task TheVersionALiveGameRunsIsAskedOfItsBridge()
    {
        using var game = new FakeGame { Plugins = [("test.flatland.plugin", "Flatland", "2.0.1")] };
        using var live = new LiveBridge(game.Url, game.Token);
        Assert.Equal("2.0.1", await WorldSession.GamePluginVersion(live, "test.flatland.plugin"));
        Assert.Null(await WorldSession.GamePluginVersion(live, "other.plugin"));
        using var older = new FakeGame();
        using var olderLive = new LiveBridge(older.Url, older.Token);
        Assert.Null(await WorldSession.GamePluginVersion(olderLive, "test.flatland.plugin"));
    }
}

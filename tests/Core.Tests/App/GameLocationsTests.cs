using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The order of the game's location lists, as its own log (Player.log) shows them added at each start
/// (GameLocations.OrderFromLog): the last run's, lists that add no location left out. And the rules read from a
/// made-up game's main scene (SceneGame), kept in a file until the game changes.
/// </summary>
public class GameLocationsTests
{
    [Fact]
    public void TheListsOrderComesFromTheLastRunInTheGamesLog()
    {
        static string Added(int locations, int vegetation) =>
            $"10/10/2026 06:59:10: Added {locations} locations, {vegetation} vegetations, 0 environments, 0 biome env-setups, 0 clutter  from main";
        var log = new List<string>
        {
            "10/10/2026 05:11:04: Zonesystem Start 3488",
            Added(3, 0), Added(27, 25), Added(25, 33),
            "[Message:HD Valheim Textures] something else",
            "10/10/2026 06:59:10: Zonesystem Start 3488",
            Added(3, 0), Added(2, 0), Added(0, 12), Added(27, 25), Added(4, 0), Added(25, 33), Added(25, 35),
            "10/10/2026 06:59:11: Added 9 locations, 1 vegetations from start",
        };
        Assert.Equal(new[] { (3, 0), (2, 0), (27, 25), (4, 0), (25, 33), (25, 35) }, GameLocations.OrderFromLog(log));
        // Two runs one after the other (no line between): the second.
        Assert.Equal(new[] { (3, 0), (5, 1), (6, 2) }, GameLocations.OrderFromLog([Added(3, 0), Added(4, 0), Added(3, 0), Added(5, 1), Added(6, 2)]));
        // No such lines: none.
        Assert.Null(GameLocations.OrderFromLog(["Zonesystem Start 1", "nothing"]));
    }

    [Fact]
    public void TheRulesAreReadFromTheMainSceneAndKept()
    {
        var order = GameLocations.ListOrder().Order;
        using var game = SceneGame.Make(order);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);
        string cache = Path.Combine(game.Root, "cache", "rules.json");

        var rules = GameLocations.Load(bundles, cache)!;
        // ZoneSystem's own (the one with most locations), the lists in the order the game adds them, the alt biome's.
        Assert.Equal(2 + order.Sum(o => o.Locations) + 1, rules.locations.Count);
        var temple = rules.locations[0];
        // Named by the manifest from its asset id; the other by m_prefabName.
        Assert.Equal("StartTemple", temple.prefab);
        Assert.Equal("Crypt_old", rules.locations[1].prefab);
        Assert.Equal((1, 1, 1, 12.5f, "g"), (temple.prioritized, temple.enable, temple.quantity, temple.exteriorRadius, temple.group));
        Assert.Equal($"L{order[0].Locations}x{order[0].Vegetation}_0", rules.locations[2].name);
        var ice = rules.locations[^1];
        Assert.Equal(("IceCave", "DeepNorth_Ice"), (ice.name, ice.altBiome));
        Assert.Null(temple.altBiome);
        var alt = Assert.Single(rules.altBiomes);
        Assert.Equal(["Crypt"], alt.blockLocationNames);
        Assert.True(File.Exists(cache));

        // Read again: from the kept file, the same rules.
        Assert.Equal(rules.ToJson(), GameLocations.Load(bundles, cache)!.ToJson());
        // A kept file of another game: read again.
        File.WriteAllText(cache, "other\n{}");
        Assert.Equal(rules.ToJson(), GameLocations.Load(bundles, cache)!.ToJson());
        // Not kept when no file is given.
        Assert.Equal(rules.locations.Count, GameLocations.Load(bundles, null)!.locations.Count);
    }

    [Fact]
    public void RulesNotAllFoundGiveNone()
    {
        // A list of the order missing (none with these counts), or no main scene at all: no rules, no error.
        using var game = SceneGame.Make([(1, 0)]);
        game.Save();
        Assert.Null(GameLocations.Load(new GameBundles(game.BundlesDir, null), null));
        using var empty = new TestGame();
        empty.Save();
        Assert.Null(GameLocations.Load(new GameBundles(empty.BundlesDir, null), null));
        // No manifest: whatever fails, none.
        using var bare = new TestGame();
        Assert.Null(GameLocations.Load(new GameBundles(bare.BundlesDir, null), null));
        Assert.True(GameLocations.UseGameRules(null).IsCompletedSuccessfully);
        Assert.Equal(2, GameLocations.Export(null));
    }
}

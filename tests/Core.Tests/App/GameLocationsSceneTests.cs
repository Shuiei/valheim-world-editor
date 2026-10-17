using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The main scene's scan (GameLocations.FindScene) and the rule set made of its parts (RuleSetOf), on a made-up game's
/// scene (SceneGame): ZoneSystem with the most locations, every LocationList of the order's counts, the alt biomes, and
/// the rule set's locations in the order of the zone, the lists and the alt biomes.
/// </summary>
public class GameLocationsSceneTests
{
    /// <summary>
    /// The scene's bundles, the main one first (as the scene's manifest lists them).
    /// </summary>
    private static readonly List<string> Scanned = [SceneGame.Main, SceneGame.Shared];

    [Fact]
    public void TheZoneSystemWithTheMostLocationsIsTakenWhateverTheBundlesOrder()
    {
        // The main ZoneSystem has two locations, the shared one (in the other bundle) one.
        using var game = SceneGame.Make([]);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);
        Assert.Equal(2, GameLocations.FindScene(bundles, Scanned, []).Zone!["Array"].Children.Count);
        Assert.Equal(2, GameLocations.FindScene(bundles, [SceneGame.Shared, SceneGame.Main], []).Zone!["Array"].Children.Count);
    }

    [Fact]
    public void EveryListOfTheSameCountsIsKept()
    {
        // Two lists of one pair of counts are both kept, as two lists.
        using var game = SceneGame.Make([(2, 1), (2, 1)]);
        game.Save();
        var scene = GameLocations.FindScene(new GameBundles(game.BundlesDir, null), Scanned, [(2, 1)]);
        Assert.Equal(2, scene.Lists[(2, 1)].Count);
    }

    [Fact]
    public void AListTheOrderDoesNotNameIsLeftOut()
    {
        // A list of (3, 0) locations, with the order naming only (2, 1): not kept.
        using var game = SceneGame.Make([(3, 0)]);
        game.Save();
        var scene = GameLocations.FindScene(new GameBundles(game.BundlesDir, null), Scanned, [(2, 1)]);
        Assert.False(scene.Lists.ContainsKey((3, 0)));
    }

    [Fact]
    public void TheAltBiomesOfTheWorldGeneratorAreFound()
    {
        using var game = SceneGame.Make([]);
        game.Save();
        var scene = GameLocations.FindScene(new GameBundles(game.BundlesDir, null), Scanned, []);
        var alt = Assert.Single(scene.Alts!["Array"].Children);
        Assert.Equal("DeepNorth_Ice", alt["m_name"].AsString);
    }

    [Fact]
    public void TheRuleSetIsTheZoneThenEachListInTheOrderThenTheAltBiomes()
    {
        // The zone's two locations, the (2, 1) list's two, the (1, 0) list's one, then the alt biome's one: six.
        var order = new[] { (2, 1), (1, 0) };
        using var game = SceneGame.Make([(1, 0), (2, 1)]);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);
        var rules = GameLocations.RuleSetOf(GameLocations.FindScene(bundles, Scanned, order), GameLocations.Manifest.Read(bundles.Folder), order);
        Assert.Equal(6, rules.locations.Count);
        Assert.Equal(["StartTemple", "Crypt_old"], rules.locations.Take(2).Select(l => l.prefab));
        Assert.Equal(["L2x1_0", "L2x1_1", "L1x0_0", "IceCave"], rules.locations.Skip(2).Select(l => l.name));
        Assert.Equal("DeepNorth_Ice", rules.locations[5].altBiome);
        var alt = Assert.Single(rules.altBiomes);
        Assert.Equal("DeepNorth_Ice", alt.name);
        Assert.Equal(["Crypt"], alt.blockLocationNames);
    }
}

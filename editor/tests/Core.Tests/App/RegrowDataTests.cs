using TerrainEditor.App;
using TerrainEditor.Editing;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Regrow with Expand World Data's vegetation file in the world's rules: the world's own rules in place of the game's,
/// one of several prefabs drawn an object, a scale by axis drawn again after the object wakes, and the mod's own
/// distance check from a centre.
/// </summary>
[Collection("World files")]
public class RegrowDataTests
{
    /// <summary>A rule that grows anywhere on land, many groups a zone.</summary>
    private static VegetationRule Open(string prefab) => new()
    {
        prefab = prefab,
        name = prefab,
        enable = 1,
        biome = (int)Heightmap.Biome.All,
        biomeArea = 7,
        min = 20f,
        max = 20f,
        minAltitude = -1000f,
        maxAltitude = 1000f,
        maxTerrainDelta = 1000f,
        scaleMin = 1f,
        scaleMax = 1f,
    };

    /// <summary>The spots Regrow places in the middle zone under these rules (null: the game's).</summary>
    private static List<Regrow.Spot> Spots(TempWorld w, IReadOnlyList<VegetationRule>? rules)
    {
        var world = w.Load();
        world.Rules = rules == null ? WorldRules.Vanilla : new WorldRules { DataInstalled = true, Vegetation = rules };
        var terrain = new TerrainService(world, null);
        return Regrow.Zones(terrain, new EditStore(world), world.Seed, 0, 0, 1, 1);
    }

    [Fact]
    public void TheWorldsOwnRulesReplaceTheGames()
    {
        using var w = new TempWorld();
        var own = Spots(w, [Open("Pine")]);
        Assert.NotEmpty(own);
        Assert.All(own, s => Assert.Equal("Pine", s.Name));
        Assert.All(own, s => Assert.Null(s.Axes));
        // The game's rules place other things; and the same again, rules or no rules.
        Assert.Contains(Spots(w, null), s => s.Name != "Pine");
        Assert.Equal(own, Spots(w, [Open("Pine")]));
    }

    [Fact]
    public void OneOfSeveralPrefabsIsDrawnAnObject()
    {
        using var w = new TempWorld();
        var several = Open("Pine");
        several.prefabs = ["Pine", "Beech1", "Birch1"];
        var spots = Spots(w, [several]);
        Assert.Equal(["Beech1", "Birch1", "Pine"], spots.Select(s => s.Name).Distinct().Order(StringComparer.Ordinal));
        // The draw moves the sequence on: the objects after the first stand elsewhere than with one prefab.
        var one = Spots(w, [Open("Pine")]);
        Assert.Equal(one.Count, spots.Count);
        Assert.NotEqual(one.Select(s => (s.X, s.Z)), spots.Select(s => (s.X, s.Z)));
    }

    [Fact]
    public void AScaleByAxisIsDrawnAgainAfterTheObjectWakes()
    {
        using var w = new TempWorld();
        var uniform = Open("Pine");
        (uniform.scaleMin, uniform.scaleMax, uniform.scaleMinAxes, uniform.scaleMaxAxes) = (0.5f, 2f, [0.5f, 0.5f, 0.5f], [2f, 2f, 2f]);
        var spots = Spots(w, [uniform]);
        Assert.NotEmpty(spots);
        Assert.All(spots, s =>
        {
            Assert.NotNull(s.Axes);
            Assert.Equal(s.Axes[0], s.Scale);
            Assert.Equal(s.Axes[0], s.Axes[1]);
            Assert.InRange(s.Scale, 0.5f, 2f);
        });
        var byAxis = Open("Pine");
        (byAxis.scaleMin, byAxis.scaleMax, byAxis.scaleMinAxes, byAxis.scaleMaxAxes, byAxis.scaleUniform) = (0.5f, 2f, [0.5f, 1f, 1.5f], [1f, 2f, 3f], 0);
        var apart = Spots(w, [byAxis]);
        Assert.Contains(apart, s => s.Axes![0] != s.Axes[1]);
        Assert.All(apart, s => Assert.InRange(s.Axes![2], 1.5f, 3f));
        // The game's own uniform scale: the draw after the object's is the mod's, so the spots after it move.
        var plain = Open("Pine");
        (plain.scaleMin, plain.scaleMax) = (0.5f, 2f);
        var game = Spots(w, [plain]);
        Assert.All(game, s => Assert.Null(s.Axes));
        Assert.NotEqual(game.Select(s => s.Scale), spots.Select(s => s.Scale));
    }

    [Fact]
    public void AnOwnCentreMeasuresTheDistanceTheModsWay()
    {
        using var w = new TempWorld();
        var near = Open("Pine");
        near.center = [40f, 30f];
        near.distance = [0f, 50f];
        var spots = Spots(w, [near]);
        Assert.NotEmpty(spots);
        // Three dimensions, the centre's second number for the height, nothing for z: as the mod computes it.
        Assert.All(spots, s => Assert.True(Math.Sqrt((s.X - 40f) * (s.X - 40f) + (s.Y - 30f) * (s.Y - 30f) + s.Z * s.Z) <= 50.01, $"{s.X}, {s.Y}, {s.Z}"));
        Assert.True(spots.Count < Spots(w, [Open("Pine")]).Count);
        var far = Open("Pine");
        far.center = [40f, 30f];
        far.distance = [50f, 0f];
        Assert.All(Spots(w, [far]), s => Assert.True(Math.Sqrt((s.X - 40f) * (s.X - 40f) + (s.Y - 30f) * (s.Y - 30f) + s.Z * s.Z) >= 49.99));
    }
}

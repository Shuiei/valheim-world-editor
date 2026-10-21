using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ground Regrow keeps clear: a location's square in its own zone (the game's check), a neighbour's wide location
/// and circles widened by the rule's own clear radius with Expand World Data, and the ground an object of a rule that
/// asks for it keeps clear of what comes after it.
/// </summary>
[Collection("World files")]
public class RegrowClearAreasTests
{
    /// <summary>A rule that grows anywhere on land, many groups a zone.</summary>
    private static VegetationRule Open(string name = "trees") => new()
    {
        prefab = "Pine",
        name = name,
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

    /// <summary>A location kind that clears this radius.</summary>
    private static SeedLocations.Rule Kind(string prefab, float radius, bool clears = true) =>
        new() { Prefab = prefab, Name = prefab, ExteriorRadius = radius, ClearArea = clears ? 1 : 0 };

    /// <summary>The world's rules with these location kinds and vegetation.</summary>
    private static WorldRules Rules(SeedLocations.Rule[] kinds, VegetationRule[]? vegetation = null, bool data = false) =>
        new() { DataInstalled = data, Vegetation = vegetation, Locations = new SeedLocations.RuleSet { Locations = [.. kinds] } };

    /// <summary>The spots Regrow places in zones 0..1 under these rules, with these location instances.</summary>
    private static List<Regrow.Spot> Spots(TempWorld w, WorldRules rules, VegetationRule rule, params (string Prefab, float X, float Z)[] instances)
    {
        var world = w.Load();
        world.Rules = rules with { Vegetation = [rule] };
        var terrain = new TerrainService(world, null);
        var areas = new Regrow.ClearAreas(terrain.Generator, instances.Select(i => (StableHash.Of(i.Prefab), i.X, i.Z)));
        return Regrow.Zones(terrain, new EditStore(world), world.Seed, 0, 0, 1, 1, areas);
    }

    [Fact]
    public void ALocationKeepsTheSquareRoundItClearInItsZone()
    {
        using var w = new TempWorld();
        var rules = Rules([Kind("Temple", 20f), Kind("Ruin", 20f, clears: false)]);
        var plain = Spots(w, rules, Open());
        var cleared = Spots(w, rules, Open(), ("Temple", 10f, 5f));
        Assert.Contains(plain, s => Math.Abs(s.X - 10f) < 20f && Math.Abs(s.Z - 5f) < 20f);
        Assert.DoesNotContain(cleared, s => Math.Abs(s.X - 10f) < 20f && Math.Abs(s.Z - 5f) < 20f);
        // Outside the square, the spots of the zone without the location... and those of the other zones stay.
        Assert.Contains(cleared, s => Math.Abs(s.X - 10f) >= 20f || Math.Abs(s.Z - 5f) >= 20f);
        Assert.Equal(plain.Where(s => Regrow.ClearAreas.ZoneOf(s) != (0, 0)), cleared.Where(s => Regrow.ClearAreas.ZoneOf(s) != (0, 0)));
        // A kind that does not clear, or one the rules do not know: nothing changes.
        Assert.Equal(plain, Spots(w, rules, Open(), ("Ruin", 10f, 5f)));
        Assert.Equal(plain, Spots(w, rules, Open(), ("Unknown", 10f, 5f)));
    }

    [Fact]
    public void WithTheModAWideNeighbourClearsToo()
    {
        var gen = SeedPreview.Generator("5DCcdIcuYJ", Rules([Kind("Wide", 40f), Kind("Small", 20f)], data: true));
        var areas = new Regrow.ClearAreas(gen, [(StableHash.Of("Wide"), 70f, 0f), (StableHash.Of("Small"), 0f, 70f)]);
        Assert.Equal([new Regrow.ClearArea(70f, 0f, 40f)], areas.For(0, 0));
        Assert.Equal([new Regrow.ClearArea(70f, 0f, 40f)], areas.For(1, 0));
        // Its own, then the wide one in the zone across the corner.
        Assert.Equal([new Regrow.ClearArea(0f, 70f, 20f), new Regrow.ClearArea(70f, 0f, 40f)], areas.For(0, 1));
        // Without the mod, only the zone's own location.
        var game = new Regrow.ClearAreas(SeedPreview.Generator("5DCcdIcuYJ", Rules([Kind("Wide", 40f)])), [(StableHash.Of("Wide"), 70f, 0f)]);
        Assert.Empty(game.For(0, 0));
        Assert.Single(game.For(1, 0));
    }

    [Fact]
    public void WithTheModsVegetationAClearAreaIsACircleWidenedByTheRule()
    {
        using var w = new TempWorld();
        static bool InSquare(Regrow.Spot s) => Math.Abs(s.X - 10f) < 20f && Math.Abs(s.Z - 5f) < 20f;
        static double Distance(Regrow.Spot s) => Math.Sqrt((s.X - 10f) * (s.X - 10f) + (s.Z - 5f) * (s.Z - 5f));
        var dense = Open();
        (dense.min, dense.max) = (200f, 200f);
        var circle = Spots(w, Rules([Kind("Temple", 20f)], data: true), dense, ("Temple", 10f, 5f)).Where(s => Regrow.ClearAreas.ZoneOf(s) == (0, 0)).ToList();
        // A circle: nothing within it, and the square's corners grow.
        Assert.All(circle, s => Assert.True(Distance(s) >= 20f));
        Assert.Contains(circle, InSquare);
        Assert.DoesNotContain(Spots(w, Rules([Kind("Temple", 20f)]), dense, ("Temple", 10f, 5f)), InSquare);
        // Widened by the rule's own clear radius.
        dense.clearRadius = 5f;
        Assert.All(Spots(w, Rules([Kind("Temple", 20f)], data: true), dense, ("Temple", 10f, 5f)).Where(s => Regrow.ClearAreas.ZoneOf(s) == (0, 0)), s => Assert.True(Distance(s) >= 25f));
    }

    [Fact]
    public void AnObjectOfTheModsRuleKeepsItsGroundClearOfWhatFollows()
    {
        using var w = new TempWorld();
        var rules = Rules([], data: true);
        var keeps = Open();
        (keeps.clearArea, keeps.clearRadius) = (1, 8f);
        var spots = Spots(w, rules, keeps);
        Assert.NotEmpty(spots);
        // Within a zone, no two of them closer than the radius (the later one stays out of the earlier one's).
        foreach (var zone in spots.GroupBy(Regrow.ClearAreas.ZoneOf))
        {
            var list = zone.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                Assert.All(list.Skip(i + 1), s => Assert.True(Math.Sqrt((s.X - list[i].X) * (s.X - list[i].X) + (s.Z - list[i].Z) * (s.Z - list[i].Z)) >= 8f));
            }
        }
        Assert.True(spots.Count < Spots(w, rules, Open()).Count);
    }
}

using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The location layout with Expand World Data installed and its location file in use: a kind wider than a zone at the
/// zone's middle, "similar" by the game's prefab and by group membership, the mod's "awayFrom" and "closeTo", the start
/// temple forced into the middle zone, and altitude limits scaled by the biome's height transform.
/// </summary>
public class SeedLocationsPlacerModTests
{
    private const int Seed = 12345;

    /// <summary>A generator with the mod installed and a location file in use (these kinds).</summary>
    private static WorldGenerator Gen(bool data = true, params SeedLocations.Rule[] kinds) =>
        WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { DataInstalled = data, Locations = data ? new SeedLocations.RuleSet { Locations = [.. kinds] } : null } });

    /// <summary>A placer for a generator; the biome map is not needed for these checks.</summary>
    private static SeedLocations.Placer Placer(WorldGenerator gen) => new(gen, () => throw new InvalidOperationException("no biome map needed"));

    /// <summary>A kind that passes the ground checks anywhere on land or sea, with these settings changed.</summary>
    private static SeedLocations.Rule Open(string prefab, float exterior = 10f, Action<SeedLocations.Rule>? set = null)
    {
        var rule = new SeedLocations.Rule
        {
            Prefab = prefab,
            Name = prefab,
            Biome = (int)Heightmap.Biome.All,
            BiomeArea = 3,
            Quantity = 1,
            Enable = 1,
            ExteriorRadius = exterior,
            MinAltitude = -1000f,
            MaxAltitude = 1000f,
            MaxTerrainDelta = 1000f,
        };
        set?.Invoke(rule);
        return rule;
    }

    [Fact]
    public void AKindWiderThanAZoneStandsAtTheZonesMiddle()
    {
        var wide = Open("Wide", exterior: 40f);
        ValheimGen.UnityEngine.Random.InitState(1);
        var placer = Placer(Gen(true, wide));
        Assert.True(placer.TrySpot(wide, Heightmap.Biome.All, (3, 2), 40f));
        var at = placer.Instances[(3, 2)];
        Assert.Equal(192f, at.X);
        Assert.Equal(128f, at.Z);
        // The game's own layout draws the spot (the draws are made either way: the next draw is the same).
        ValheimGen.UnityEngine.Random.InitState(1);
        var plain = Placer(Gen(false, wide));
        Assert.True(plain.TrySpot(wide, Heightmap.Biome.All, (3, 2), 40f));
        Assert.NotEqual(192f, plain.Instances[(3, 2)].X);
        Assert.Equal(ValheimGen.UnityEngine.Random.Value, ValheimGen.UnityEngine.Random.Value);
    }

    [Fact]
    public void ClonesAndGroupMembersCountAsSimilar()
    {
        var a = Open("Crypt2:A", set: r => { r.BasePrefab = "Crypt2"; r.Groups = ["crypts"]; });
        var b = Open("Crypt2:B", set: r => { r.BasePrefab = "Crypt2"; r.MinDistanceFromSimilar = 500f; });
        var byGroup = Open("Other", set: r => { r.Group = "crypts"; r.MinDistanceFromSimilar = 500f; });
        var placer = Placer(Gen(true, a, b, byGroup));
        placer.Register(a, new Vector3(0f, 30f, 0f));
        Assert.False(placer.RightNeighbours(b, new Vector3(100f, 30f, 0f)));
        Assert.True(placer.RightNeighbours(b, new Vector3(600f, 30f, 0f)));
        Assert.False(placer.RightNeighbours(byGroup, new Vector3(100f, 30f, 0f)));
        // The game's own check tells the clones apart (different names) and knows no membership.
        var plain = Placer(Gen(false));
        plain.Register(a, new Vector3(0f, 30f, 0f));
        Assert.True(plain.RightNeighbours(b, new Vector3(100f, 30f, 0f)));
        Assert.True(plain.RightNeighbours(byGroup, new Vector3(100f, 30f, 0f)));
    }

    [Fact]
    public void AwayFromAndCloseToRulesAreCheckedByNameAndGroup()
    {
        var a = Open("Crypt2:A", set: r => { r.BasePrefab = "Crypt2"; r.Groups = ["crypts"]; });
        var away = Open("Hut", set: r => { r.Group = "_1"; r.MinDistanceFromSimilar = 1f; r.AwayFrom = [new SeedLocations.DistanceRule("crypts", 300f)]; });
        var close = Open("Camp", set: r => { r.GroupMax = "_2"; r.MaxDistanceFromSimilar = 1f; r.CloseTo = [[new SeedLocations.DistanceRule("Crypt2:A", 500f)]]; });
        var closeTwo = Open("Camp2", set: r => { r.GroupMax = "_3"; r.MaxDistanceFromSimilar = 1f; r.CloseTo = [[new SeedLocations.DistanceRule("Crypt2:A", 500f)], [new SeedLocations.DistanceRule("Nowhere", 500f)]]; });
        var placer = Placer(Gen(true, a, away, close, closeTwo));
        placer.Register(a, new Vector3(0f, 30f, 0f));
        Assert.False(placer.RightNeighbours(away, new Vector3(100f, 30f, 0f)));
        Assert.True(placer.RightNeighbours(away, new Vector3(400f, 30f, 0f)));
        Assert.True(placer.RightNeighbours(close, new Vector3(100f, 30f, 0f)));
        Assert.False(placer.RightNeighbours(close, new Vector3(600f, 30f, 0f)));
        // Every row needs a match.
        Assert.False(placer.RightNeighbours(closeTwo, new Vector3(100f, 30f, 0f)));
    }

    [Fact]
    public void TheStartTempleIsForcedIntoTheMiddleZoneWhenNonePlaced()
    {
        // A start kind no spot passes (no biome): the game's layout places none, the mod puts one in the middle zone.
        var start = Open(SeedLocations.Placer.StartPrefab, set: r => { r.Biome = 0; r.CenterFirst = 1; r.Prioritized = 1; });
        var gen = Gen(true, start);
        var placer = Placer(gen);
        placer.Generate(start);
        var placed = Assert.Single(placer.Instances.Values);
        Assert.Equal((0, 0), placer.Instances.Keys.Single());
        Assert.Equal(0f, placed.X);
        Assert.Equal(0f, placed.Z);
        Assert.Equal(gen.GetHeight(0f, 0f), placed.Y);
        // Another kind is not forced, and nothing is without the mod.
        var other = Open("Other", set: r => { r.Biome = 0; r.CenterFirst = 1; });
        placer.Generate(other);
        Assert.Single(placer.Instances);
        var plain = Placer(Gen(false));
        plain.Generate(start);
        Assert.Empty(plain.Instances);
    }

    [Fact]
    public void AltitudeLimitsAreScaledByTheBiomesTransformWhenAsked()
    {
        var plainGen = Gen(false);
        // A Meadows spot between 10 and 20 m above the sea.
        var (x, z) = GeneratorPoints.Rings(300, 3000, 100, 24).First(p => plainGen.GetBiome(p.X, p.Z) == Heightmap.Biome.Meadows && plainGen.GetHeight(p.X, p.Z) - 30f is > 10f and < 20f);
        var rule = Open("Hut", set: r => { r.MinAltitude = 10f; r.MaxAltitude = 1000f; });
        var spot = new Vector3(x, 0f, z);
        Assert.True(Placer(plainGen).OnRightGround(rule, Heightmap.Biome.All, ref spot, out _));
        // Meadows doubled: the limit of 10 m becomes 20 m, past the spot.
        var meadows = BiomeTable.Vanilla.Of(Heightmap.Biome.Meadows)! with { HasData = true, Height = new HeightTransform { Multiplier = 2f } };
        var table = new BiomeTable([.. BiomeTable.Vanilla.Biomes.Select(b => b.Value == Heightmap.Biome.Meadows ? meadows : b)]);
        var scaledGen = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { DataInstalled = true, BiomeData = true, Biomes = table, ScaleLocationAltitude = true } });
        Assert.False(Placer(scaledGen).OnRightGround(rule, Heightmap.Biome.All, ref spot, out _));
        // Not asked, or the biome file off: the limit as written.
        var unscaled = WorldGenerator.Create(new World { Seed = Seed, WorldGenVersion = 2, Rules = new WorldRules { DataInstalled = true, BiomeData = true, Biomes = table } });
        Assert.True(Placer(unscaled).OnRightGround(rule, Heightmap.Biome.All, ref spot, out _));
    }
}

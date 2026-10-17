using ValheimGen;
using Xunit;
using static WorldEditor.Tests.SeedLocationsSupport;

namespace WorldEditor.Tests;

/// <summary>
/// The seed's location rules as the editor reads and writes them (SeedLocations.RuleSet, the alt biomes), and the random biome
/// and biome map the layout draws from.
/// </summary>
public class SeedLocationsRulesTests
{
    [Fact]
    public void TheRulesRoundTripAndCanBeSwapped()
    {
        Fixtures.UseLocations();
        var copy = SeedLocations.Current;
        Assert.True(SeedLocations.Available);
        var again = SeedLocations.RuleSet.FromJson(copy.ToJson());
        Assert.Equal(copy.locations.Select(l => (l.prefab, l.quantity, l.biome, l.altBiome)), again.locations.Select(l => (l.prefab, l.quantity, l.biome, l.altBiome)));
        Assert.Equal(copy.ToJson(), again.ToJson());
        Assert.Equal("StartTemple", SeedLocations.Ordered[0].prefab);
        // The tests' copy keeps only what the editor shows, and trimming it again changes nothing.
        Assert.All(copy.locations, l => Assert.True(l.prioritized != 0 && l.enable != 0 && l.quantity != 0, l.prefab));
        Assert.Equal(copy.ToJson(), SeedLocations.Prioritized(copy).ToJson());
    }

    [Fact]
    public void AltBiomesRoundTripWithTheRules()
    {
        var rules = new SeedLocations.RuleSet
        {
            locations = [new SeedLocations.Rule { prefab = "MadeUpRuin", name = "Made-up ruin" }],
            altBiomes = [new SeedLocations.AltBiomeRule { name = "MadeUpMeadows", blockLocationNames = ["Made-up ruin"] }],
        };
        var again = SeedLocations.RuleSet.FromJson(rules.ToJson());
        var alt = Assert.Single(again.altBiomes);
        Assert.Equal("MadeUpMeadows", alt.name);
        Assert.Equal(["Made-up ruin"], alt.blockLocationNames);
        Assert.Equal(rules.ToJson(), again.ToJson());
        Assert.Throws<System.Text.Json.JsonException>(() => SeedLocations.RuleSet.FromJson("null"));
    }

    [Fact]
    public void ARandomBiomeIsOneOfTheSet()
    {
        ValheimGen.UnityEngine.Random.InitState(7);
        var two = Heightmap.Biome.Meadows | Heightmap.Biome.Swamp;
        for (int i = 0; i < 20; i++)
        {
            var b = SeedLocations.BiomeMap.RandomBiome(two);
            Assert.True(b is Heightmap.Biome.Meadows or Heightmap.Biome.Swamp, b.ToString());
        }
        // A single biome, or none: no draw.
        Assert.Equal(Heightmap.Biome.Plains, SeedLocations.BiomeMap.RandomBiome(Heightmap.Biome.Plains));
        Assert.Equal(Heightmap.Biome.None, SeedLocations.BiomeMap.RandomBiome(Heightmap.Biome.None));
    }

    [Fact]
    public void ABiomeTheMapHasNoPointOfGivesNoPoint()
    {
        // A rule of no biome indexed an empty list, and an unknown biome flag the list at -1: both threw, and the whole
        // layout of the seed was dropped.
        var map = new SeedLocations.BiomeMap(Seed(), true, TestContext.Current.CancellationToken);
        Assert.Null(map.RandomPoint(Heightmap.Biome.None, false));
        Assert.Null(map.RandomPoint((Heightmap.Biome)4096, true));
        Assert.NotNull(map.RandomPoint(Heightmap.Biome.Meadows, true));
    }
}

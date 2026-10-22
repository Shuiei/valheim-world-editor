using ValheimGen;
using Xunit;
using static WorldEditor.Tests.AltBiomeRules;
using Alt = ValheimGen.AltBiomeRule;
using Layout = ValheimGen.SeedLocations.AltBiomeLayout;

namespace WorldEditor.Tests;

/// <summary>
/// The alt biome layout (AltBiomeWorldData's areas and GenerateAltBiomes): a seed's areas and alt biomes as the game
/// made them, each condition an area must meet, the minimum and the most an alt biome is given, and what the layout
/// lets stand where.
/// </summary>
public class SeedLocationsAltBiomeLayoutTests(AltBiomeSeed seed) : IClassFixture<AltBiomeSeed>
{
    /// <summary>How many areas each alt biome gets on the seed.</summary>
    private IReadOnlyDictionary<string, int> Given(params Alt[] alts) => new Layout(seed.Map, seed.Gen, alts, TestContext.Current.CancellationToken).Given;

    [Fact]
    public void TheSeedsAreasAndAltBiomesAreThoseTheGameMade()
    {
        var lines = Layout.DumpLines(seed.Map, seed.Gen, Fixtures.AltBiomes());
        Assert.Equal(File.ReadAllLines(Path.Combine(Fixtures.Root, "altbiomes-reference.txt")), lines);
    }

    [Fact]
    public void AnAltBiomeGetsAtLeastItsLeastAndAtMostItsMost()
    {
        var least = Everywhere("Least");
        (least.MinAmountSpawned, least.MaxAmountSpawned, least.Chance) = (3, 5, 0f);
        var most = Everywhere("Most");
        most.MaxAmountSpawned = 4;
        var off = Everywhere("Off");
        off.Enabled = false;
        var given = Given(least, most, off);
        Assert.Equal(3, given["Least"]);
        Assert.Equal(4, given["Most"]);
        Assert.False(given.ContainsKey("Off"));
    }

    [Fact]
    public void EachConditionOfAnAreaKeepsTheAltBiomeOut()
    {
        static Alt With(Action<Alt> set)
        {
            var alt = Everywhere("Test");
            set(alt);
            return alt;
        }
        int all = Given(Everywhere("Test"))["Test"];
        Assert.True(all > 100);
        Assert.False(Given(With(a => a.MinDistanceFromCenter = 1e9f)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.MinEdgeSize = int.MaxValue)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.MaxEdgeSize = 0)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.MinAvgHeight = 1e9f)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.MaxAvgHeight = -1e9f)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.AboveWorldX = 1e9f)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.BelowWorldX = -1e9f)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.AboveWorldY = 1e9f)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.BelowWorldY = -1e9f)).ContainsKey("Test"));
        Assert.False(Given(With(a => a.Biome = (int)Heightmap.Biome.None)).ContainsKey("Test"));
        Assert.InRange(Given(With(a => a.Biome = (int)Heightmap.Biome.Meadows))["Test"], 1, all - 1);
    }

    [Fact]
    public void NeighboursCountAsTheGameReadsThem()
    {
        // Any neighbour asked for: never given (the game looks for a neighbour of biome 0).
        var asks = Everywhere("Asks");
        asks.RequireNeighbor = (int)Heightmap.Biome.Swamp;
        Assert.False(Given(asks).ContainsKey("Asks"));
        // Forbidding the Black Forest keeps the alt biome from areas beside the mountains (index 4 read as biome 4).
        int all = Given(Everywhere("Test"))["Test"];
        var noForest = Everywhere("Test");
        noForest.NotNeighbor = (int)Heightmap.Biome.BlackForest;
        Assert.InRange(Given(noForest)["Test"], 1, all - 1);
        // Forbidding the plains counts no neighbour at all (index 5 is no biome).
        var noPlains = Everywhere("Test");
        noPlains.NotNeighbor = (int)Heightmap.Biome.Plains;
        Assert.Equal(all, Given(noPlains)["Test"]);
    }

    [Fact]
    public void IncompatibleAltBiomesAreNeverGivenTogether()
    {
        var first = Everywhere("First");
        var second = Everywhere("Second");
        second.IncompatibleAltBiomes = ["First"];
        var third = Everywhere("Third");
        var given = Given(first, second, third);
        Assert.False(given.ContainsKey("Second"));
        // Either side naming the other is enough.
        first.IncompatibleAltBiomes = ["Third"];
        given = Given(first, Everywhere("Third"));
        Assert.False(given.ContainsKey("Third"));
    }

    [Fact]
    public void TheLayoutTellsWhatMayStandWhere()
    {
        var dark = Everywhere("Dark");
        dark.Biome = (int)Heightmap.Biome.Meadows;
        dark.BlockLocationNames = ["WoodFarm1"];
        dark.BlockVegetationNames = ["beech1_meadows"];
        var layout = new Layout(seed.Map, seed.Gen, [dark], TestContext.Current.CancellationToken);
        var (inside, outside) = (Find(layout, true), Find(layout, false));
        Assert.Equal(["Dark"], layout.At(inside.X, inside.Z).Select(a => a.Name));
        Assert.True(layout.LetsVegetation("Dark", "fireflies", inside.X, inside.Z));
        Assert.False(layout.LetsVegetation("Dark", "fireflies", outside.X, outside.Z));
        Assert.False(layout.LetsVegetation("Other", "fireflies", inside.X, inside.Z));
        Assert.False(layout.LetsVegetation(null, "beech1_meadows", inside.X, inside.Z));
        Assert.True(layout.LetsVegetation(null, "beech1_meadows", outside.X, outside.Z));
        Assert.False(layout.LetsLocation(new LocationRule { Name = "WoodFarm1" }, inside.X, inside.Z));
        Assert.True(layout.LetsLocation(new LocationRule { Name = "WoodFarm1" }, outside.X, outside.Z));
        Assert.True(layout.LetsLocation(new LocationRule { Name = "Crypt", AltBiome = "Dark" }, inside.X, inside.Z));
        Assert.False(layout.LetsLocation(new LocationRule { Name = "Crypt", AltBiome = "Dark" }, outside.X, outside.Z));
        // Past the map's edge, the nearest point's area.
        Assert.Equal(layout.At(1e6f, 1e6f), layout.At(seed.Map.MapToWorld(seed.Map.Size - 1), seed.Map.MapToWorld(seed.Map.Size - 1)));
    }

    /// <summary>A world point in an area the alt biome has (or not).</summary>
    private static (float X, float Z) Find(Layout layout, bool has)
    {
        for (float z = -9000f; z < 9000f; z += 64f)
        {
            for (float x = -9000f; x < 9000f; x += 64f)
            {
                if (layout.At(x, z).Count > 0 == has)
                {
                    return (x, z);
                }
            }
        }
        throw new InvalidOperationException("no such point");
    }

    [Fact]
    public void AWorldsLayoutIsMadeOnceForItsAltBiomes()
    {
        List<Alt> alts = [Everywhere("Test")];
        int made = 0;
        SeedLocations.BiomeMap Map()
        {
            made++;
            return seed.Map;
        }
        var gen = TerrainEditor.App.SeedPreview.Generator(AltBiomeSeed.Name);
        var first = Layout.Of(gen, alts, Map, TestContext.Current.CancellationToken);
        Assert.Same(first, Layout.Of(gen, alts, Map, TestContext.Current.CancellationToken));
        Assert.Equal(1, made);
        Assert.NotSame(first, Layout.Of(gen, [Everywhere("Test")], Map, TestContext.Current.CancellationToken));
        Assert.Equal(2, made);
    }
}

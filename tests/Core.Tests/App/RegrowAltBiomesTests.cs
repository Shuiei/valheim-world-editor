using TerrainEditor.App;
using TerrainEditor.Editing;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Regrow under the world's alt biomes: a rule an alt biome adds grows only in its areas, a rule an alt biome blocks
/// grows nowhere it lies, and the other rules are as without alt biomes.
/// </summary>
[Collection("World files")]
public class RegrowAltBiomesTests
{
    /// <summary>A rule that grows anywhere on land, many groups a zone.</summary>
    private static VegetationRule Open(string name, string? alt = null) => new()
    {
        prefab = "Pine",
        name = name,
        altBiome = alt,
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

    /// <summary>An alt biome given to every area of the world, keeping these rules out.</summary>
    private static SeedLocations.AltBiomeRule Everywhere(string name, params string[] blocks)
    {
        var alt = AltBiomeRules.Everywhere(name);
        alt.BlockVegetationNames = [.. blocks];
        return alt;
    }

    /// <summary>The spots Regrow places in the middle zones under these rules and alt biomes.</summary>
    private static List<Regrow.Spot> Spots(TempWorld w, VegetationRule rule, params SeedLocations.AltBiomeRule[] alts)
    {
        var world = w.Load();
        world.Rules = new WorldRules { Vegetation = [rule], Locations = new SeedLocations.RuleSet { AltBiomes = [.. alts] } };
        var terrain = new TerrainService(world, null);
        return Regrow.Zones(terrain, new EditStore(world), world.Seed, 0, 0, 1, 1);
    }

    [Fact]
    public void AnAltBiomesRuleGrowsInItsAreasOnly()
    {
        using var w = new TempWorld();
        var plain = Spots(w, Open("trees"));
        Assert.NotEmpty(plain);
        // Its alt biome everywhere: it grows as a rule of every area would.
        Assert.Equal(plain, Spots(w, Open("trees", "Dark"), Everywhere("Dark")));
        // Another alt biome everywhere, or none: nowhere.
        Assert.Empty(Spots(w, Open("trees", "Dark"), Everywhere("Light")));
        Assert.Empty(Spots(w, Open("trees", "Dark")));
    }

    [Fact]
    public void ARuleAnAltBiomeBlocksGrowsNowhereItLies()
    {
        using var w = new TempWorld();
        Assert.Empty(Spots(w, Open("trees"), Everywhere("Dark", "trees")));
        Assert.Equal(Spots(w, Open("trees")), Spots(w, Open("trees"), Everywhere("Dark", "rocks")));
    }
}

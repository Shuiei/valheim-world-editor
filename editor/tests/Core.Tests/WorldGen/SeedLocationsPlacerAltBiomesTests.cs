using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The location layout under the world's alt biomes: a kind an alt biome adds stands only in its areas, a kind an
/// alt biome blocks stands nowhere it lies, and the other kinds are as without alt biomes.
/// </summary>
public class SeedLocationsPlacerAltBiomesTests
{
    /// <summary>An alt biome given to every area of the world, keeping these kinds out.</summary>
    private static AltBiomeRule Everywhere(string name, params string[] blocks)
    {
        var alt = AltBiomeRules.Everywhere(name);
        alt.BlockLocationNames = [.. blocks];
        return alt;
    }

    /// <summary>The traders' camps laid out on seed 5DCcdIcuYJ as the world's only kind, of this alt biome, under these alt biomes.</summary>
    private static List<(float X, float Z)> Camps(string? altBiome, params AltBiomeRule[] alts)
    {
        var kind = LocationRuleSet.FromJson(File.ReadAllText(Path.Combine(Fixtures.Root, "locations.json"))).Locations.First(l => l.Prefab == "Vendor_BlackForest");
        kind.AltBiome = altBiome;
        var rules = new WorldRules { Locations = new LocationRuleSet { Locations = [kind], AltBiomes = [.. alts] } };
        return [.. SeedLocations.Place(TerrainEditor.App.SeedPreview.Generator("5DCcdIcuYJ", rules), SeedLocations.Which.All, parallel: true, cancel: TestContext.Current.CancellationToken).Select(p => (p.X, p.Z))];
    }

    [Fact]
    public void AKindOfAnAltBiomeStandsInItsAreasOnly()
    {
        var plain = Camps(null);
        Assert.NotEmpty(plain);
        Assert.Equal(plain, Camps("Dark", Everywhere("Dark")));
        Assert.Empty(Camps("Dark", Everywhere("Light")));
        Assert.Empty(Camps("Dark"));
    }

    [Fact]
    public void AKindAnAltBiomeBlocksStandsNowhereItLies()
    {
        Assert.Empty(Camps(null, Everywhere("Dark", "Vendor_BlackForest")));
        Assert.Equal(Camps(null), Camps(null, Everywhere("Dark", "Crypt2")));
    }
}

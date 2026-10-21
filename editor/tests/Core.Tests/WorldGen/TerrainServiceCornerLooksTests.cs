using ValheimGen;
using Xunit;
using static WorldEditor.Tests.AltBiomeRules;

namespace WorldEditor.Tests;

/// <summary>
/// <see cref="TerrainService.CornerLooks"/>: the biome whose ground look each zone corner takes, the terrain texture
/// override of its area's alt biome when one has it, else its own biome.
/// </summary>
[Collection("World files")]
public class TerrainServiceCornerLooksTests
{
    /// <summary>The middle zone's corners with the world's alt biomes being these (its own location rules).</summary>
    private static (int[] Biomes, int[] Looks) Corners(TempWorld w, params AltBiomeRule[] alts)
    {
        var world = w.Load();
        world.Rules = new WorldRules { DataInstalled = true, Locations = new LocationRuleSet { AltBiomes = [.. alts] } };
        var terrain = new TerrainService(world, null);
        return (terrain.CornerBiomes(0, 0), terrain.CornerLooks(0, 0));
    }

    [Fact]
    public void AnAltBiomesOverrideGivesItsAreasAnotherBiomesLook()
    {
        using var w = new TempWorld();
        var ashy = Everywhere("Ashy");
        ashy.TerrainTextureOverride = (int)Heightmap.Biome.AshLands;
        var (_, looks) = Corners(w, ashy);
        Assert.All(looks, l => Assert.Equal((int)Heightmap.Biome.AshLands, l));
    }

    [Fact]
    public void WithoutAnOverrideTheCornersLookIsTheirBiome()
    {
        using var w = new TempWorld();
        var (biomes, looks) = Corners(w, Everywhere("Plain"));
        Assert.Equal(biomes, looks);
    }
}

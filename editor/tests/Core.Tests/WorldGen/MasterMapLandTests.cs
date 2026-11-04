using TerrainEditor.App;
using ValheimGen;
using Xunit;
using K = TerrainEditor.App.MasterMap.Kind;

namespace WorldEditor.Tests;

/// <summary>
/// A drawn world (MasterMapWorlds, MasterMapLand): the generator takes the continents and the biomes from the master map
/// and keeps the rest the game's. Biomes are where they were drawn, mountains rise above the land round them, rivers and
/// lakes are under water in the biome of their banks, the sea deepens away from the coast, past the world's edge is
/// the sea, and the start temple is looked for from the start drawn.
/// </summary>
public class MasterMapLandTests
{
    /// <summary>
    /// An island: Meadows 4 km round the middle in shallow sea, a mountain 1.2 km wide in the east, a Black Forest in the
    /// west with a lake in it, a river running north from the middle, Ashlands in the south.
    /// </summary>
    private static MasterMap Island()
    {
        var map = new MasterMap();
        void Disc(float x, float z, float metres, K kind)
        {
            var (c, r) = MasterMap.CellOf(x, z);
            map.Disc(c, r, metres / MasterMap.CellMetres, kind);
        }
        Disc(0, 0, 5000, K.Sea);
        Disc(0, 0, 4000, K.Meadows);
        Disc(2500, 0, 1200, K.Mountain);
        Disc(-2500, 0, 1000, K.BlackForest);
        Disc(-2500, 0, 200, K.Lake);
        Disc(0, -3000, 700, K.AshLands);
        for (float z = 300; z < 3000; z += 10)
        {
            Disc(0, z, 20, K.River);
        }
        map.Start = MasterMap.CellOf(-1000, 1000);
        return map;
    }

    private static readonly Lazy<WorldGenerator> Gen = new(() => SeedPreview.Generator("Drawn", MasterMapWorlds.Rules(Island())));

    [Fact]
    public void BiomesAreWhereTheyWereDrawn()
    {
        var gen = Gen.Value;

        Assert.Equal(Heightmap.Biome.Meadows, gen.GetBiome(-500f, -500f));
        Assert.Equal(Heightmap.Biome.Mountain, gen.GetBiome(2500f, 0f));
        Assert.Equal(Heightmap.Biome.BlackForest, gen.GetBiome(-2500f, 600f));
        Assert.Equal(Heightmap.Biome.AshLands, gen.GetBiome(0f, -3000f));
        Assert.Equal(Heightmap.Biome.Ocean, gen.GetBiome(0f, 4600f));
        Assert.Equal(Heightmap.Biome.Ocean, gen.GetBiome(8000f, 8000f));
        Assert.True(gen.IsAshlands(0f, -3000f));
        Assert.False(gen.IsAshlands(0f, -9000f));
    }

    [Fact]
    public void RiversAndLakesAreUnderWaterInTheBiomeOfTheirBanks()
    {
        var gen = Gen.Value;

        Assert.Equal(Heightmap.Biome.Meadows, gen.GetBiome(0f, 1500f));
        Assert.True(gen.GetHeight(0f, 1500f) < gen.Rules.WaterLevel);
        Assert.True(gen.GetHeight(300f, 1500f) > gen.Rules.WaterLevel);
        Assert.Equal(Heightmap.Biome.BlackForest, gen.GetBiome(-2500f, 0f));
        Assert.True(gen.GetHeight(-2500f, 0f) < gen.Rules.WaterLevel);
    }

    [Fact]
    public void MountainsRiseAboveTheLandRoundThemAndTheSeaDeepensAwayFromTheCoast()
    {
        var gen = Gen.Value;

        float meadows = gen.GetHeight(-1000f, -1000f), mountain = gen.GetHeight(2500f, 0f);
        Assert.InRange(meadows, gen.Rules.WaterLevel, gen.Rules.WaterLevel + 60f);
        Assert.True(mountain > meadows + 80f, $"mountain {mountain} m, meadows {meadows} m");
        Assert.True(gen.GetHeight(0f, 4500f) > gen.GetHeight(0f, 8000f));
        Assert.True(gen.GetHeight(0f, 8000f) < gen.Rules.WaterLevel - 50f);
    }

    [Fact]
    public void TheSameMapMakesTheSameLandAndAnotherMapOther()
    {
        var a = MasterMapWorlds.Rules(Island());
        var b = MasterMapWorlds.Rules(Island());
        var other = Island();
        other.Disc(1500, 1500, 3, K.Plains);

        Assert.Equal(a.Stamp, b.Stamp);
        Assert.NotEqual(a.Stamp, MasterMapWorlds.Rules(other).Stamp);
        Assert.Equal(SeedPreview.Generator("Drawn", a).GetHeight(123f, 456f), SeedPreview.Generator("Drawn", b).GetHeight(123f, 456f));
    }

    [Fact]
    public void TheStartTempleIsLookedForFromTheStartDrawn()
    {
        Fixtures.UseLocations();

        var gen = SeedPreview.Generator("Drawn", MasterMapWorlds.Rules(Island()));

        var start = Assert.Single(SeedLocations.Place(gen, SeedLocations.Which.Start, parallel: true, TestContext.Current.CancellationToken), p => p.Rule.Prefab == "StartTemple");
        Assert.InRange(MathF.Sqrt((start.X + 1000f) * (start.X + 1000f) + (start.Z - 1000f) * (start.Z - 1000f)), 0f, 300f);
    }
}

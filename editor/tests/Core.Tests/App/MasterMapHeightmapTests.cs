using DrawnWorlds;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;
using K = TerrainEditor.App.MasterMap.Kind;

namespace WorldEditor.Tests;

/// <summary>
/// A master map from a heightmap: the heights kept, the sea under the sea level, the mountains where the land rises
/// past the game's mountain height, and the game's own biomes between; the heights saved beside the picture and kept
/// in a drawn world's file come back the same, and painted kinds hold the heights to what they need.
/// </summary>
public class MasterMapHeightmapTests
{
    /// <summary>A heightmap: black at the edges, rising to white in the middle (a round island with a peak).</summary>
    private static byte[] Island(int n = 256)
    {
        ushort[] g = new ushort[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                double dx = (x - n / 2.0) / (n / 2.0), dy = (y - n / 2.0) / (n / 2.0);
                g[y * n + x] = (ushort)(Math.Clamp(1 - Math.Sqrt(dx * dx + dy * dy) * 1.5, 0, 1) * 65535);
            }
        }
        return Png.WriteGray16(n, n, g);
    }

    private static readonly Lazy<MasterMap> Map = new(() => MasterMap.FromHeightmap(Island(), 0.1f, "Seed1"));

    [Fact]
    public void TheSeaIsUnderTheSeaLevelAndTheMountainsWhereItIsHigh()
    {
        var map = Map.Value;
        int middle = MasterMap.Size / 2;

        Assert.NotNull(map.Heights);
        Assert.Equal(K.DeepSea, map.At(5, 5));
        Assert.Equal(K.Mountain, map.At(middle, middle));
        Assert.Null(map.Start);
        // Between, on land, the game's own biomes.
        K ring = map.At(middle + MasterMap.Size / 6, middle);
        Assert.False(MasterMap.IsWater(ring));
        Assert.NotEqual(K.Mountain, ring);
    }

    [Fact]
    public void HeightsSavedBesideThePictureComeBackTheSame()
    {
        var map = Map.Value;

        ushort[]? back = MasterMap.ReadHeights(map.HeightsPng());

        Assert.Equal(map.Heights, back);
        Assert.Null(MasterMap.ReadHeights(Island(64)));
    }

    [Fact]
    public void HeightsKeptInAWorldFileComeBackTheSame()
    {
        var map = Map.Value;

        byte[] kept = DrawnLand.Encode(map.Cells, -1, -1, map.Heights);
        DrawnLand.Decode(kept, out _, out _, out ushort[]? heights);

        Assert.Equal(map.Heights, heights);
        Assert.Equal(2, BitConverter.ToInt32(kept, 0));
        Assert.Equal(1, BitConverter.ToInt32(DrawnLand.Encode(map.Cells, -1, -1), 0));
    }

    [Fact]
    public void PaintedKindsHoldTheHeightsToWhatTheyNeed()
    {
        var map = Map.Value.Copy();
        int middle = MasterMap.Size / 2;
        // Land painted in the deep sea, and a lake on the peak.
        map.Disc(40, middle, 20, K.Meadows);
        map.Disc(middle, middle, 20, K.Lake);
        var gen = SeedPreview.Generator("Seed1", MasterMapWorlds.Rules(map));

        var (landX, landZ) = MasterMap.CenterOf(40, middle);
        var (lakeX, lakeZ) = MasterMap.CenterOf(middle, middle);
        Assert.True(gen.GetHeight(landX, landZ) > gen.Rules.WaterLevel);
        Assert.True(gen.GetHeight(lakeX, lakeZ) < gen.Rules.WaterLevel);
    }

    [Fact]
    public void AKeptHeightIsWithinFourMillimetresOfTheGround()
    {
        foreach (double c in new[] { -0.3, 0.0, 0.15, 0.4, 0.9 })
        {
            Assert.InRange(DrawnLand.ContinentOf(DrawnLand.HeightOf(c)) - c, -2e-5, 2e-5);
        }
    }
}

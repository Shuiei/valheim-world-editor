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
        K ring = map.At(middle + MasterMap.Size / 4, middle);
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
        // Land painted in the deep sea, a lake on the peak, a mountain on low land.
        map.Disc(100, middle, 60, K.Meadows);
        map.Disc(middle, middle, 20, K.Lake);
        map.Disc(middle + MasterMap.Size / 4, middle, 40, K.Mountain);
        var land = MasterMapWorlds.Rules(map).Generator!.Land!;

        // The continents the drawing gives the game (its own rivers then cut through them as anywhere).
        var (landX, landZ) = MasterMap.CenterOf(100, middle);
        var (lakeX, lakeZ) = MasterMap.CenterOf(middle, middle);
        var (peakX, peakZ) = MasterMap.CenterOf(middle + MasterMap.Size / 4, middle);
        Assert.True(land.Continent(landX, landZ) > 0.2f, $"{land.Continent(landX, landZ)}");
        Assert.True(land.Continent(lakeX, lakeZ) < 0.15f, $"{land.Continent(lakeX, lakeZ)}");
        Assert.True(land.Continent(peakX, peakZ) > 0.4f, $"{land.Continent(peakX, peakZ)}");
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

using TerrainEditor.App;
using Xunit;
using static TerrainEditor.App.SeedPreview;

namespace WorldEditor.Tests;

/// <summary>
/// Counting a preview grid's cells: only cells whose centres lie inside the world's circle count, and of those only the
/// land cells count, with the land cells of each biome counted by its index.
/// </summary>
public class SeedMeasureCountLandTests
{
    /// <summary>The cells inside the world, the land cells inside it, and the land cells of each biome.</summary>
    private static (int Inside, int Land, int[] Counts) CountLand(byte[] biome, int size, float cell) =>
        ((int Inside, int Land, int[] Counts))PrivateCall.Static(typeof(SeedMeasure), "CountLand", biome, size, cell, SeedPreview.Radius)!;

    [Fact]
    public void OnlyCellsWhoseCentresLieInsideTheWorldCircleAreCounted()
    {
        // Four by four cells of half the world's radius: the four corner cells' centres lie at 0.75 radius on both
        // axes, outside the circle; the other twelve lie inside it.
        byte[] allOcean = new byte[16];
        Array.Fill(allOcean, Ocean);
        var (inside, land, counts) = CountLand(allOcean, 4, Radius / 2);
        Assert.Equal(12, inside);
        Assert.Equal(0, land);
        Assert.All(counts, c => Assert.Equal(0, c));
    }

    [Fact]
    public void ALandCellInsideTheCircleIsCountedUnderItsBiome()
    {
        byte[] biome = new byte[16];
        Array.Fill(biome, Ocean);
        // Cell (1, 1) has its centre at (-0.25, -0.25) radius: inside. Cell (0, 0) is a corner outside the circle, so
        // its biome does not count.
        biome[1 * 4 + 1] = 2;
        biome[0] = 1;
        var (inside, land, counts) = CountLand(biome, 4, Radius / 2);
        Assert.Equal(12, inside);
        Assert.Equal(1, land);
        Assert.Equal(1, counts[2]);
        Assert.Equal(0, counts[1]);
    }
}

using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// 16-bit heightmaps: written and read back, with every PNG row filter, and the ground of a real area. The world
/// generator keeps static state: one world at a time.
/// </summary>
[Collection("World files")]
public class HeightmapsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Gray16RoundTripsWithEveryFilter(int filter)
    {
        int w = 37, h = 23;
        ushort[] px = [.. Enumerable.Range(0, w * h).Select(i => (ushort)((i * 7919) % 65536))];
        byte[] png = Png.WriteGray16(w, h, px, new Dictionary<string, string> { ["k"] = "v" }, filter);
        Png.Image img = Png.Read(png);
        Assert.Equal((w, h), (img.Width, img.Height));
        Assert.Equal("v", img.Text["k"]);
        for (int i = 0; i < px.Length; i++)
        {
            Assert.Equal(px[i], (ushort)Math.Round(img.Values[i] * 65535f));
        }
    }

    [Fact]
    public void SomethingElseIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => Png.Read([1, 2, 3, 4, 5, 6, 7, 8, 9]));
    }

    [Fact]
    public void TheGroundOfAnAreaComesBackFromItsHeightmap()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        var terrain = new ValheimGen.TerrainService(world, new TerrainModifiers(world));
        var (width, h, heights) = HeightGrid.Read(terrain, edits, -1, -1, 1, 1);
        Assert.Equal((193, 193), (width, h));
        byte[] png = Heightmaps.Encode(width, h, heights, "test", out float min, out float max);
        Png.Image img = Png.Read(png);
        Assert.Equal(min.ToString("R", System.Globalization.CultureInfo.InvariantCulture), img.Text[Heightmaps.MinKey]);
        float[] back = Heightmaps.Resample(img, width, h);
        // 16 bits over the height range: well under a centimetre.
        float worst = Enumerable.Range(0, heights.Length).Max(i => MathF.Abs(min + back[i] * (max - min) - heights[i]));
        Assert.True(worst < 0.01f, $"worst difference {worst} m");
        // North is at the top of the picture: its first row is the area's northern edge.
        Assert.Equal(heights[(h - 1) * width], min + img.Values[0] * (max - min), 2);
    }
}

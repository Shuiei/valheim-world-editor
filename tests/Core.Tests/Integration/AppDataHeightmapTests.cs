using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Heightmap pictures: the editor's heights, and pictures without them.
/// </summary>
[Collection("World files")]
public class AppDataHeightmapTests
{
    [Fact]
    public void FlatGroundStillMakesAHeightmap()
    {
        float[] flat = [.. Enumerable.Repeat(30f, 4)];
        byte[] png = Heightmaps.Encode(2, 2, flat, "flat", out float min, out float max);
        Assert.Equal(30f, min);
        Assert.Equal(30.01f, max, 4);
        var p = Heightmaps.Read(png, 2, 2);
        Assert.Equal((2, 2, 30f, "flat"), (p.Width, p.Height, p.Min!.Value, p.Area));
        Assert.All(p.Values, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void APictureWithoutTheEditorsHeightsIsJustBrightness()
    {
        byte[] png = Png.WriteGray16(2, 1, [0, 65535]);
        var p = Heightmaps.Read(png, 3, 1);
        Assert.Null(p.Min);
        Assert.Null(p.Max);
        Assert.Null(p.Area);
        Assert.Equal(new[] { 0f, 0.5f, 1f }, p.Values);
        // One point: the picture's first pixel.
        Assert.Equal(new[] { 0f }, Heightmaps.Read(png, 1, 1).Values);
    }
}

using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Png.WriteRgba8: an 8-bit RGBA picture is written with its size and colour type in its header, and reads back at the
/// same size.
/// </summary>
public class PngRgbaTests
{
    [Fact]
    public void AnRgbaPictureHasItsSizeAndEightBitRgbaHeader()
    {
        // Three by two pixels, four bytes each.
        byte[] rgba = new byte[3 * 2 * 4];
        byte[] png = Png.WriteRgba8(3, 2, rgba);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(3, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
        Assert.Equal(2, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
        Assert.Equal(8, png[24]);
        Assert.Equal(6, png[25]);
    }

    [Fact]
    public void AnRgbaPictureReadsBackAtItsSize()
    {
        byte[] rgba = new byte[2 * 1 * 4];
        var image = Png.Read(Png.WriteRgba8(2, 1, rgba));

        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(2, image.Values.Length);
    }
}

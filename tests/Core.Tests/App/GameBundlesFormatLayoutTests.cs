using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How many bytes a texture format takes per pixel block, and whether it is stored in blocks: the block formats are
/// 8 bytes (DXT1, BC4) or 16 bytes (DXT5, BC5, BC6H, BC7) for each 4 by 4 block, the plain ones a fixed size per pixel.
/// </summary>
public sealed class GameBundlesFormatLayoutTests
{
    /// <summary>DXT1 and BC4 take 8 bytes a block.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(26)]
    public void EightByteBlockFormatsAreBlocks(int format) =>
        Assert.Equal((8, true), GameBundles.FormatLayout(format));

    /// <summary>DXT5, BC5, BC6H and BC7 take 16 bytes a block.</summary>
    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(27)]
    public void SixteenByteBlockFormatsAreBlocks(int format) =>
        Assert.Equal((16, true), GameBundles.FormatLayout(format));

    /// <summary>Alpha8 and R8 take one byte a pixel, not in blocks.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    public void OneBytePixelFormatsAreNotBlocks(int format) =>
        Assert.Equal((1, false), GameBundles.FormatLayout(format));

    /// <summary>RGB24 takes three bytes a pixel.</summary>
    [Fact]
    public void Rgb24TakesThreeBytesAPixel() =>
        Assert.Equal((3, false), GameBundles.FormatLayout(3));

    /// <summary>RGBA32, ARGB32 and BGRA32 take four bytes a pixel.</summary>
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(14)]
    public void FourBytePixelFormatsAreNotBlocks(int format) =>
        Assert.Equal((4, false), GameBundles.FormatLayout(format));

    /// <summary>A format the table does not know has no size.</summary>
    [Fact]
    public void AnUnknownFormatHasNoSize() =>
        Assert.Equal((0, false), GameBundles.FormatLayout(99));
}

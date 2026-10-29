using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The texture format a Unity graphics format number stands for: each block-compressed number (a pair of them for the
/// formats with two variants) maps to the format's code, and a number the editor does not know maps to 0.
/// </summary>
public class GameBundlesTextureFormatTests
{
    /// <summary>The texture format code of a graphics format number.</summary>
    private static int FormatOf(int graphicsFormat) => (int)PrivateCall.Static(typeof(GameBundles), "FromGraphicsFormat", graphicsFormat)!;

    [Theory]
    [InlineData(96, 10)]
    [InlineData(97, 10)]
    [InlineData(98, 11)]
    [InlineData(99, 11)]
    [InlineData(100, 12)]
    [InlineData(101, 12)]
    [InlineData(102, 26)]
    [InlineData(103, 26)]
    [InlineData(104, 27)]
    [InlineData(105, 27)]
    [InlineData(106, 24)]
    [InlineData(107, 24)]
    [InlineData(108, 25)]
    [InlineData(109, 25)]
    [InlineData(8, 4)]
    [InlineData(4, 4)]
    public void EachGraphicsFormatNumberMapsToItsTextureFormat(int graphicsFormat, int format)
    {
        Assert.Equal(format, FormatOf(graphicsFormat));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(95)]
    [InlineData(110)]
    public void AGraphicsFormatTheEditorDoesNotKnowMapsToZero(int graphicsFormat)
    {
        Assert.Equal(0, FormatOf(graphicsFormat));
    }
}

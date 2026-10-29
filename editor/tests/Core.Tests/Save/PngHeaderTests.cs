using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The parts of a PNG read on their own: the header's fields, the channels of each colour type, and a pixel's
/// brightness.
/// </summary>
public class PngHeaderTests
{
    private static byte[] Header(int width, int height, byte depth, byte color, byte interlace = 0) =>
    [
        0, 0, 0, (byte)width, 0, 0, 0, (byte)height, depth, color, 0, 0, interlace,
    ];

    [Fact]
    public void AHeaderGivesItsSizeDepthAndColour()
    {
        var h = Png.ParseHeader(Header(3, 2, 8, 2));

        Assert.Equal(new Png.PngHeader(3, 2, 8, 2), h);
    }

    [Fact]
    public void AShortHeaderIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => Png.ParseHeader(new byte[12]));
    }

    [Fact]
    public void AnInterlacedHeaderIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => Png.ParseHeader(Header(1, 1, 8, 2, interlace: 1)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    [InlineData(4, 2)]
    [InlineData(6, 4)]
    public void EachColourTypeHasItsChannels(int color, int channels)
    {
        Assert.Equal(channels, Png.ChannelsOf(color));
    }

    [Fact]
    public void APaletteColourTypeIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => Png.ChannelsOf(3));
    }

    [Fact]
    public void AEightBitChannelIsItsFractionOfTheMaximum()
    {
        Assert.Equal(128f / 255f, Png.Sample([0x80], 0, 0, 1, 8, 255f), 6);
    }

    [Fact]
    public void ASixteenBitChannelReadsItsTwoBytesBigEndian()
    {
        Assert.Equal(0x1234 / 65535f, Png.Sample([0x12, 0x34], 0, 0, 2, 16, 65535f), 6);
    }
}

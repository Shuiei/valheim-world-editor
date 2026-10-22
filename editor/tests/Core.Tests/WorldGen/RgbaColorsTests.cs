using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>The rules' colours as the generator and the map use them (RgbaColors): the mask colour's channels, and bytes rounded and held to 0..255.</summary>
public class RgbaColorsTests
{
    [Fact]
    public void AColourKeepsItsChannelsAsTheGeneratorsColour()
    {
        var c = new Rgba(0.1f, 0.2f, 0.3f, 0.4f).ToColor();
        Assert.Equal((0.1f, 0.2f, 0.3f, 0.4f), (c.R, c.G, c.B, c.A));
    }

    [Fact]
    public void ItsBytesAreRoundedAndHeldToTheirRange()
    {
        Assert.Equal(((byte)51, (byte)128, (byte)255), new Rgba(0.2f, 0.5f, 1f, 1f).Bytes());
        // Outside 0..1 (a file may say so): held to the ends.
        Assert.Equal(((byte)0, (byte)255, (byte)0), new Rgba(-0.5f, 1.5f, 0f, 1f).Bytes());
    }
}

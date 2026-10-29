using System.Numerics;
using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's look colours are read from sRGB (gamma) values into linear ones: the sRGB curve below its straight
/// part and above it, and a colour's three channels one by one.
/// </summary>
public class GameLookGlPaletteTests
{
    /// <summary>
    /// One sRGB channel as linear, through the look's own conversion.
    /// </summary>
    private static float Lin(float c) => (float)typeof(GameLookGl).GetMethod("Lin", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [c])!;

    [Fact]
    public void BlackStaysBlackAndWhiteStaysWhite()
    {
        Assert.Equal(0f, Lin(0f), 6);
        Assert.Equal(1f, Lin(1f), 6);
    }

    [Fact]
    public void ADarkChannelBelowTheStraightPartIsDividedBy12Point92()
    {
        Assert.Equal(0.02f / 12.92f, Lin(0.02f), 6);
    }

    [Fact]
    public void AMidGreyAboveTheStraightPartIsTheGammaCurve()
    {
        // sRGB 0.5 is about 0.214 linear.
        Assert.Equal(0.2140f, Lin(0.5f), 3);
    }

    [Fact]
    public void AColourIsConvertedChannelByChannel()
    {
        var linear = (Vector3)typeof(GameLookGl).GetMethod("Srgb", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [0f, 0.5f, 1f])!;

        Assert.Equal(0f, linear.X, 6);
        Assert.Equal(0.2140f, linear.Y, 3);
        Assert.Equal(1f, linear.Z, 6);
    }
}

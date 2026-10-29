using System.Numerics;
using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The hue, saturation and value colour conversion of the object overlays: the six hues at full saturation, grey at
/// zero saturation, and a value above one scaling the colour.
/// </summary>
public class GlViewHsvTests
{
    /// <summary>
    /// The colour of a hue (0..1), saturation and value, through the view's own conversion.
    /// </summary>
    private static Vector3 HsvToRgb(float h, float s, float v) =>
        (Vector3)typeof(GlView).GetMethod("HsvToRgb", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [h, s, v])!;

    [Fact]
    public void HueZeroAtFullSaturationAndValueIsRed()
    {
        AssertColour(new Vector3(1, 0, 0), HsvToRgb(0f, 1f, 1f));
    }

    [Fact]
    public void AThirdOfTheHueWheelIsGreen()
    {
        AssertColour(new Vector3(0, 1, 0), HsvToRgb(1f / 3f, 1f, 1f));
    }

    [Fact]
    public void TwoThirdsOfTheHueWheelIsBlue()
    {
        AssertColour(new Vector3(0, 0, 1), HsvToRgb(2f / 3f, 1f, 1f));
    }

    [Fact]
    public void ZeroSaturationIsGreyAtTheValue()
    {
        AssertColour(new Vector3(0.5f, 0.5f, 0.5f), HsvToRgb(0.4f, 0f, 0.5f));
    }

    [Fact]
    public void AValueAboveOneScalesTheColour()
    {
        AssertColour(new Vector3(1.2f, 0, 0), HsvToRgb(0f, 1f, 1.2f));
    }

    /// <summary>
    /// Asserts that two colours match to within a small tolerance on each channel.
    /// </summary>
    private static void AssertColour(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 5);
        Assert.Equal(expected.Y, actual.Y, 5);
        Assert.Equal(expected.Z, actual.Z, 5);
    }
}

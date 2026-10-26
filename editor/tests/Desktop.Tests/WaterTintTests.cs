using System.Numerics;
using TerrainEditor.Desktop;
using ValheimGen;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// <see cref="WaterTint"/>: the game look water's colours, the editor's own unless Expand World Data's water colours
/// give others (linear, the surface's as a hue).
/// </summary>
public class WaterTintTests
{
    [Fact]
    public void WithoutColoursTheWaterIsTheEditorsOwn()
    {
        Assert.Equal(WaterTint.Own, WaterTint.Of(WaterColours.None));
        Assert.Equal(Vector4.Zero, WaterTint.Own.Shore);
        Assert.Equal(Vector3.One, WaterTint.Own.Surface);
    }

    [Fact]
    public void EachColourGivenReplacesItsOwn()
    {
        var tint = WaterTint.Of(new WaterColours(Top: new Rgba(1f, 0f, 0f, 1f), Shallow: new Rgba(0f, 2f, -1f, 1f)));
        Assert.Equal(new Vector3(1f, 0f, 0f), tint.Shallow);
        Assert.Equal(WaterTint.Own.Deep, tint.Deep);
        // Channels are taken between 0 and 1; the tint is full.
        Assert.Equal(new Vector4(0f, 1f, 0f, 1f), tint.Shore);
        Assert.Equal(Vector3.One, tint.Surface);
        var deep = WaterTint.Of(new WaterColours(Bottom: new Rgba(0.5f, 0.5f, 0.5f, 1f)));
        Assert.Equal(MathF.Pow(0.5f, 2.2f), deep.Deep.X, 5);
        Assert.Equal(WaterTint.Own.Shallow, deep.Shallow);
    }

    [Fact]
    public void TheSurfaceColourTintsByItsHueOnly()
    {
        var tint = WaterTint.Of(new WaterColours(Surface: new Rgba(0.5f, 0.25f, 0f, 1f)));
        Assert.Equal(1f, tint.Surface.X, 5);
        Assert.Equal(MathF.Pow(0.5f, 2.2f), tint.Surface.Y, 5);
        Assert.Equal(0f, tint.Surface.Z, 5);
        // Black has no hue: no tint.
        Assert.Equal(Vector3.One, WaterTint.Of(new WaterColours(Surface: new Rgba(0f, 0f, 0f, 1f))).Surface);
    }
}

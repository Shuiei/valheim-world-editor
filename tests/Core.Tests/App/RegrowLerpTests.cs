using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Regrow's interpolation between two heights, as Unity's Mathf.Lerp: the fraction is clamped to 0..1, so a fraction
/// past either end gives that end's value.
/// </summary>
public class RegrowLerpTests
{
    /// <summary>
    /// The interpolated value between <paramref name="a"/> and <paramref name="b"/> at fraction <paramref name="t"/>.
    /// </summary>
    private static float Lerp(float a, float b, float t) => (float)PrivateCall.Static(typeof(Regrow), "Lerp", a, b, t)!;

    [Fact]
    public void AFractionOfZeroGivesTheFirstValue()
    {
        Assert.Equal(2f, Lerp(2f, 6f, 0f));
    }

    [Fact]
    public void AFractionOfOneGivesTheSecondValue()
    {
        Assert.Equal(6f, Lerp(2f, 6f, 1f));
    }

    [Fact]
    public void AFractionInBetweenIsTheShareOfTheWayFromFirstToSecond()
    {
        Assert.Equal(3f, Lerp(2f, 6f, 0.25f));
    }

    [Fact]
    public void AFractionBelowZeroIsClampedToTheFirstValue()
    {
        Assert.Equal(2f, Lerp(2f, 6f, -3f));
    }

    [Fact]
    public void AFractionAboveOneIsClampedToTheSecondValue()
    {
        Assert.Equal(6f, Lerp(2f, 6f, 4f));
    }
}

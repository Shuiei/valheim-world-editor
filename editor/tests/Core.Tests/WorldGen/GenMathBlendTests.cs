using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The eased and blended ramps: Smooth's step, the eased blend between two values, and the overlay blend.
/// </summary>
public class GenMathBlendTests
{
    [Fact]
    public void SmoothStepsFromZeroToOneAndEasesInTheMiddle()
    {
        Assert.Equal(0f, GenMath.Smooth(0f, 1f, 0f));
        Assert.Equal(0.5f, GenMath.Smooth(0f, 1f, 0.5f));
        Assert.Equal(1f, GenMath.Smooth(0f, 1f, 1f));
    }

    [Fact]
    public void SmoothBlendIsTheStartAtZeroAndTheEndAtOne()
    {
        Assert.Equal(0.0, GenMath.SmoothBlend(0.0, 10.0, 0.0));
        Assert.Equal(10.0, GenMath.SmoothBlend(0.0, 10.0, 1.0));
    }

    [Fact]
    public void SmoothBlendIsHalfWayAtHalf()
    {
        Assert.Equal(5.0, GenMath.SmoothBlend(0.0, 10.0, 0.5));
    }

    [Fact]
    public void SmoothBlendHoldsItsTimeToTheRange()
    {
        Assert.Equal(10.0, GenMath.SmoothBlend(0.0, 10.0, 2.0));
        Assert.Equal(0.0, GenMath.SmoothBlend(0.0, 10.0, -2.0));
    }

    [Fact]
    public void OverlayIsMultiplyBelowAHalfAndScreenAbove()
    {
        Assert.Equal(0.25, GenMath.Overlay(0.25, 0.5));
        Assert.Equal(0.75, GenMath.Overlay(0.75, 0.5));
    }
}

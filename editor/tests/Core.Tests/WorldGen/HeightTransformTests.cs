using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World Data's height transform: the defaults leave an altitude alone; the multiplier, delta and depth
/// multiplier move it; what lies past the maximum or minimum is softened by the excess factor, in its sign's direction.
/// </summary>
public class HeightTransformTests
{
    [Fact]
    public void TheDefaultsLeaveAnAltitudeAlone()
    {
        Assert.True(HeightTransform.None.IsIdentity);
        Assert.Equal(12.5f, HeightTransform.None.Apply(12.5f));
        Assert.Equal(-7.25f, HeightTransform.None.Apply(-7.25f));
        Assert.False(new HeightTransform { Delta = 1f }.IsIdentity);
    }

    [Fact]
    public void TheMultiplierAndDeltaMoveTheAltitudeAndTheDepthMultiplierWhatIsUnderWater()
    {
        var t = new HeightTransform { Multiplier = 2f, Delta = 5f, DepthMultiplier = 3f };
        Assert.Equal(25f, t.Apply(10f));
        // -10 times 2 plus 5 is -15, under the water, times 3.
        Assert.Equal(-45f, t.Apply(-10f));
    }

    [Fact]
    public void WhatLiesPastTheLimitsIsSoftenedByTheExcessFactor()
    {
        var t = new HeightTransform { Maximum = 100f, Minimum = -50f };
        // 100 past the maximum: the square root of it (the factor 0.5) over the maximum.
        Assert.Equal(110f, t.Apply(200f));
        Assert.Equal(-50f - MathF.Sqrt(50f), t.Apply(-100f), 3);
        Assert.Equal(60f, t.Apply(60f));
        // A negative factor takes the excess the other way.
        var inward = new HeightTransform { Maximum = 100f, ExcessFactor = -0.5f };
        Assert.Equal(90f, inward.Apply(200f));
    }
}

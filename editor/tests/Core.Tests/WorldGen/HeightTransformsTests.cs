using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A biome's height transform as the generator applies it (HeightTransforms.Apply): the defaults leave an altitude
/// alone; the multiplier, delta and depth multiplier move it; what lies past the maximum or minimum is softened by the
/// excess factor, in its sign's direction, to the bit as the game computes it.
/// </summary>
public class HeightTransformsTests
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

    [Fact]
    public void TheExcessIsSoftenedToTheBitAsTheGameComputesIt()
    {
        var t = new HeightTransform { Multiplier = 1.3f, Delta = 0.25f, Maximum = 100f, ExcessFactor = 0.7f };
        // Each step stored as a float, the power as Unity's Mathf.Pow takes it (in double, rounded to a float).
        float h = (float)((double)(float)((double)150f * 1.3f) + 0.25f);
        float expected = (float)(100.0 + (float)Math.Pow((float)((double)h - 100f), 0.7f));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(t.Apply(150f)));
        // Below the minimum, the same the other way, with the depth multiplier first.
        var deep = new HeightTransform { DepthMultiplier = 2f, Minimum = -40f, ExcessFactor = 0.7f };
        float d = -60f;
        float below = (float)(-40.0 - (float)Math.Pow((float)(-40.0 - d), 0.7f));
        Assert.Equal(BitConverter.SingleToInt32Bits(below), BitConverter.SingleToInt32Bits(deep.Apply(-30f)));
    }
}

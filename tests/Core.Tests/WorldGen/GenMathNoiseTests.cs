using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The Perlin noise helpers: each overload reads the same noise, and layers add up weighted by gain.
/// </summary>
public class GenMathNoiseTests
{
    [Fact]
    public void TheDoubleNoiseIsTheFloatNoiseOfTheRoundedCoordinates()
    {
        Assert.Equal(GenMath.Noise(0.5f, 0.25f), GenMath.Noise(0.5, 0.25));
    }

    [Fact]
    public void TheFloatNoiseIsUnitys()
    {
        Assert.Equal(UnityPerlin.Noise(1.5f, -2.5f), GenMath.Noise(1.5f, -2.5f));
    }

    [Fact]
    public void NoLayersMakesNoNoise()
    {
        Assert.Equal(0f, GenMath.Layers(new Vector2(1, 2), 0, 2f, 0.5f));
        Assert.Equal(0.0, GenMath.Layers(new Vector2(1, 2), 0, 2.0, 0.5));
    }

    [Fact]
    public void OneLayerIsThePlainNoise()
    {
        var p = new Vector2(3.5f, 1.25f);

        Assert.Equal(GenMath.Noise(p.X, p.Y), GenMath.Layers(p, 1, 2f, 0.5f));
    }

    [Fact]
    public void ALayerOfAWorldPositionReadsItsXAndZ()
    {
        var p = new Vector3(3.5f, 99f, 1.25f);

        Assert.Equal(GenMath.Layers(new Vector2(3.5f, 1.25f), 3, 2f, 0.5f), GenMath.Layers(p, 3, 2f, 0.5f));
    }

    [Fact]
    public void TheDoubleLayersAddTheSecondLayerWeightedByGain()
    {
        var p = new Vector2(0.5f, 0.5f);
        double first = GenMath.Noise(0.5, 0.5);
        double second = GenMath.Noise(0.5 * 2.0, 0.5 * 2.0);

        Assert.Equal(first + 0.5 * second, GenMath.Layers(p, 2, 2.0, 0.5), 6);
    }
}

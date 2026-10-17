using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Simplex noise at one point: deterministic for a seed, finite and bounded, and different from another seed.
/// </summary>
public class FastNoiseSimplexTests
{
    [Fact]
    public void TheSamePointAndSeedGiveTheSameValue()
    {
        var noise = new FastNoise(1337);

        Assert.Equal(noise.SingleSimplex(1337, 0.37, 1.91), noise.SingleSimplex(1337, 0.37, 1.91));
    }

    [Fact]
    public void TheValueStaysWithinTheSimplexBound()
    {
        var noise = new FastNoise(1337);
        for (int i = 0; i < 50; i++)
        {
            double v = noise.SingleSimplex(1337, i * 0.173, i * -0.311);
            Assert.InRange(v, -1.5, 1.5);
        }
    }

    [Fact]
    public void ADifferentSeedGivesADifferentValue()
    {
        var noise = new FastNoise(1337);

        Assert.NotEqual(noise.SingleSimplex(1, 0.37, 1.91), noise.SingleSimplex(2, 0.37, 1.91));
    }
}

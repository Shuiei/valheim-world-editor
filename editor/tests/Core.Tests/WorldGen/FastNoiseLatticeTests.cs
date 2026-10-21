using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The lattice of grid points the noise is built on: the hash per point, its value (-1..1) and its gradient.
/// </summary>
public class FastNoiseLatticeTests
{
    [Fact]
    public void TheHashIsTheSameForTheSameSeedAndPoint()
    {
        Assert.Equal(FastNoise.Hash2D(1337, 3, -4), FastNoise.Hash2D(1337, 3, -4));
    }

    [Fact]
    public void TheHashChangesWithTheSeed()
    {
        Assert.NotEqual(FastNoise.Hash2D(1, 3, -4), FastNoise.Hash2D(2, 3, -4));
    }

    [Fact]
    public void AGridPointsValueStaysBetweenMinusOneAndOne()
    {
        for (int x = -3; x <= 3; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                double v = FastNoise.ValCoord2D(1337, x, y);
                Assert.InRange(v, -1.0, 1.0);
            }
        }
    }

    [Fact]
    public void AtItsOwnPointTheGradientIsZero()
    {
        // The offset from the point itself is nothing, so the gradient's dot product is nothing.
        Assert.Equal(0.0, FastNoise.GradCoord2D(1337, 5, 9, 0.0, 0.0));
    }

    [Fact]
    public void TheGradientIsLinearInTheOffset()
    {
        double once = FastNoise.GradCoord2D(1337, 5, 9, 1.0, 0.0);
        double twice = FastNoise.GradCoord2D(1337, 5, 9, 2.0, 0.0);

        Assert.Equal(2 * once, twice, 12);
    }
}

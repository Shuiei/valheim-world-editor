using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Cellular noise: the distance a cell point is measured by (each function), and the cell distance or value at a point.
/// </summary>
public class FastNoiseCellularTests
{
    [Fact]
    public void EuclideanDistanceIsTheSquaredLength()
    {
        Assert.Equal(25.0, FastNoise.CellDistance(FastNoise.CellularDistanceFunction.Euclidean, 3.0, 4.0));
    }

    [Fact]
    public void ManhattanDistanceIsTheSumOfTheSizes()
    {
        Assert.Equal(7.0, FastNoise.CellDistance(FastNoise.CellularDistanceFunction.Manhattan, -3.0, 4.0));
    }

    [Fact]
    public void NaturalDistanceIsManhattanPlusSquaredEuclidean()
    {
        Assert.Equal(7.0 + 25.0, FastNoise.CellDistance(FastNoise.CellularDistanceFunction.Natural, -3.0, 4.0));
    }

    [Fact]
    public void ADistanceNoFunctionAsksForIsEuclidean()
    {
        Assert.Equal(25.0, FastNoise.CellDistance((FastNoise.CellularDistanceFunction)99, 3.0, 4.0));
    }

    [Fact]
    public void TheDistanceToTheNearestCellIsNeverNegative()
    {
        var noise = new FastNoise(1337);
        noise.SetCellularReturnType(FastNoise.CellularReturnType.Distance);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(noise.SingleCellular(i * 0.37, i * -0.21) >= 0.0);
        }
    }

    [Fact]
    public void TheCellValueIsTheValueOfTheNearestCell()
    {
        var noise = new FastNoise(1337);
        noise.SetCellularReturnType(FastNoise.CellularReturnType.CellValue);

        double v = noise.SingleCellular(0.5, 0.5);

        Assert.InRange(v, -1.0, 1.0);
        Assert.Equal(v, noise.SingleCellular(0.5, 0.5));
    }
}

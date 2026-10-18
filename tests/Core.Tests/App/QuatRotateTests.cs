using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// GameCatalogues.Quat.Rotate: a vector turned by a rotation (q v q^-1), counter-clockwise about the axis.
/// </summary>
public class QuatRotateTests
{
    [Fact]
    public void TheRootFrameLeavesAVectorAsItIs()
    {
        var turned = GameCatalogues.Quat.Root.Rotate(1, 2, 3);

        Assert.Equal((1.0, 2.0, 3.0), turned);
    }

    [Fact]
    public void AQuarterTurnAboutZTurnsXIntoY()
    {
        double half = Math.Sqrt(0.5);
        var quarter = new GameCatalogues.Quat(0, 0, half, half);

        var (x, y, z) = quarter.Rotate(1, 0, 0);

        Assert.Equal(0, x, 12);
        Assert.Equal(1, y, 12);
        Assert.Equal(0, z, 12);
    }

    [Fact]
    public void AQuarterTurnAboutZTurnsYIntoMinusX()
    {
        double half = Math.Sqrt(0.5);
        var quarter = new GameCatalogues.Quat(0, 0, half, half);

        var (x, y, z) = quarter.Rotate((0.0, 1.0, 0.0));

        Assert.Equal(-1, x, 12);
        Assert.Equal(0, y, 12);
        Assert.Equal(0, z, 12);
    }
}

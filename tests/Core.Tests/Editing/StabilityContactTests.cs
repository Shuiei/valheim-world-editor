using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The grid helpers that find which pieces can touch: a sphere holds a piece's boxes, and a sphere only visits the grid
/// cells it reaches while there are not too many of them.
/// </summary>
public class StabilityContactTests
{
    [Fact]
    public void ASphereHoldsABoxWithItsRadiusGrown()
    {
        var box = new Stability.Obb(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(1, 1, 1));

        var (c, r) = Stability.Sphere([box]);

        Assert.Equal(Vector3.Zero, c);
        Assert.Equal(MathF.Sqrt(3) * 1.001f + 0.01f, r, 4);
    }

    [Fact]
    public void ASmallSphereVisitsEachCellItReaches()
    {
        int calls = 0;

        bool inside = Stability.InCells((Vector3.Zero, 1f), _ => calls++);

        Assert.True(inside);
        Assert.Equal(8, calls);
    }

    [Fact]
    public void AHugeSphereVisitsNoCellAndSaysSo()
    {
        int calls = 0;

        bool inside = Stability.InCells((Vector3.Zero, 1000f), _ => calls++);

        Assert.False(inside);
        Assert.Equal(0, calls);
    }
}

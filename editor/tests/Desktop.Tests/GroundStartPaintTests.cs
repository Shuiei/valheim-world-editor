using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Starting a point's paint in a ground: a point with no paint of its own takes the base mask as its paint and is marked as
/// painted; a point already painted keeps its paint.
/// </summary>
public class GroundStartPaintTests
{
    [Fact]
    public void APointWithNoPaintOfItsOwnTakesTheBaseMaskAndIsMarkedPainted()
    {
        var g = new Ground(2, 2, 0, 0, 1, 1);
        float[] mask = [0.1f, 0.2f, 0.3f, 0.4f];
        mask.CopyTo(g.BaseMask, 4);
        g.StartPaint(1);
        Assert.Equal(mask, g.Paint[4..8]);
        Assert.Equal(1, g.PMod[1]);
    }

    [Fact]
    public void APointAlreadyPaintedKeepsItsPaint()
    {
        var g = new Ground(2, 2, 0, 0, 1, 1);
        g.BaseMask[4] = 0.5f;
        g.Paint[4] = 0.9f;
        g.PMod[1] = 1;
        g.StartPaint(1);
        Assert.Equal(0.9f, g.Paint[4]);
    }
}

using System.Numerics;
using Avalonia;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Lines through points (Lines): the smooth curve goes through every point, about a metre between its own, each
/// stretch named by the point it starts from; the point and the stretch under the mouse are found within their reach.
/// </summary>
public class LinesTests
{
    /// <summary>A view that shows grid point (x, z) at pixel (x, z).</summary>
    private static Point? Flat(Vector2 g) => new Point(g.X, g.Y);

    [Fact]
    public void TheCurveGoesThroughEveryPoint()
    {
        Vector2[] pts = [new(0, 0), new(10, 0), new(10, 10)];
        var curve = Lines.CatmullRom(pts);
        Assert.Equal(21, curve.Count);
        Assert.Equal((pts[0], 0), curve[0]);
        Assert.Equal((pts[1], 1), curve[10]);
        Assert.Equal((pts[2], 1), curve[^1]);
    }

    [Fact]
    public void ThePointAndStretchUnderTheMouseAreFound()
    {
        Vector2[] pts = [new(0, 0), new(100, 0), new(100, 100)];
        Assert.Equal(1, Lines.PointNear(pts, new Point(105, 3), Flat));
        Assert.Equal(-1, Lines.PointNear(pts, new Point(50, 0), Flat));
        // As CatmullRom names them: each point by the stretch it starts, the last by the one it ends.
        (Vector2, int)[] dense = [(pts[0], 0), (pts[1], 1), (pts[2], 1)];
        Assert.Equal(0, Lines.StretchNear(dense, new Point(50, 7), Flat));
        Assert.Equal(1, Lines.StretchNear(dense, new Point(95, 50), Flat));
        Assert.Equal(-1, Lines.StretchNear(dense, new Point(50, 9), Flat));
    }
}

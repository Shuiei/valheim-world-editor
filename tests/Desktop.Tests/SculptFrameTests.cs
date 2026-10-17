using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a sculpt frame reads before it changes anything: the heights around the brush, the smoothing average, and the
/// erosion window of an erode stroke.
/// </summary>
public class SculptFrameTests
{
    [Fact]
    public void TheSnapshotHoldsTheBrushRectangleAndOnePointAround()
    {
        var g = new Ground(5, 5, 0, 0, 2);
        float[] snap = Sculpt.TakeSnapshot(g, 1, 3, 1, 3);
        Assert.Equal(25, snap.Length);
        Assert.Equal(g.HeightOf(0), snap[0]);
        Assert.Equal(g.HeightOf(2 * 5 + 2), snap[2 * 5 + 2]);
    }

    [Fact]
    public void TheSmoothingAverageIsTheMeanOfTheNineNeighbours()
    {
        var f = new Sculpt.Frame(0.1f, [.. Enumerable.Range(1, 9).Select(i => (float)i)], 3, 1, 1, null);
        Assert.Equal(5f, Sculpt.SmoothAverage(f, 1, 1));
    }

    [Fact]
    public void AFlatSnapshotAveragesToItsHeight()
    {
        var f = new Sculpt.Frame(0.1f, [.. Enumerable.Repeat(4f, 9)], 3, 1, 1, null);
        Assert.Equal(4f, Sculpt.SmoothAverage(f, 1, 1));
    }

    [Fact]
    public void AnErodeFrameWithNoRateReturnsTheWindowAroundTheBrushAndChangesNothing()
    {
        var g = new Ground(7, 7, 0, 0, 2);
        float[] before = [.. Enumerable.Range(0, 49).Select(g.HeightOf)];
        var s = new Stroke { Tool = BrushTool.Erode, Start = g.Snapshot() };
        (int X0, int Z0, int X1, int Z1) r = Sculpt.ErodeFrame(g, new Brush(), s, 2, 2, 2, 0, null);
        Assert.Equal((-1, -1, 7, 7), r);
        Assert.False(s.Clamped);
        float[] after = [.. Enumerable.Range(0, 49).Select(g.HeightOf)];
        Assert.Equal(before, after);
    }
}

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
        var snap = HeightsBefore.Take(g, 1, 1, 3, 3, GroundOps.BrushSmoothReach);
        Assert.Equal(25, snap.Count);
        Assert.Equal(g.HeightOf(0), snap.At(0));
        Assert.Equal(g.HeightOf(2 * 5 + 2), snap.At(2 * 5 + 2));
    }

    [Fact]
    public void TheSmoothingAverageIsTheMeanOfTheNineNeighbours()
    {
        // A 3 × 3 ground holding 1 to 9: the middle's nine neighbours average 5.
        var g = new Ground(3, 3, 0, 0, 2);
        for (int i = 0; i < 9; i++)
        {
            g.Base[i] = i + 1;
        }
        Assert.Equal(5f, HeightsBefore.Take(g, 1, 1, 1, 1, 1).Average(4, GroundOps.BrushSmoothReach));
    }

    [Fact]
    public void AFlatSnapshotAveragesToItsHeight()
    {
        var g = new Ground(3, 3, 0, 0, 2);
        Array.Fill(g.Base, 4f);
        Assert.Equal(4f, HeightsBefore.Take(g, 1, 1, 1, 1, 1).Average(4, GroundOps.BrushSmoothReach));
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

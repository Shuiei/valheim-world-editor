using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The grid points a stroke or a stamp may change: the points of its rectangle, with the locked edge of the ground left out.
/// </summary>
public class SculptGridTests
{
    [Fact]
    public void TheLockedEdgeIsLeftOut()
    {
        var g = new Ground(5, 5, 0, 0, 2);
        var pts = Sculpt.Unlocked(g, 0, 0, 4, 4).ToList();
        Assert.Equal(9, pts.Count);
        Assert.All(pts, p => Assert.InRange(p.Gx, 1, 3));
    }

    [Fact]
    public void PointsComeRowByRow()
    {
        var g = new Ground(5, 5, 0, 0, 2);
        var pts = Sculpt.Unlocked(g, 1, 1, 3, 2).ToList();
        Assert.Equal(new[] { (1, 1), (2, 1), (3, 1), (1, 2), (2, 2), (3, 2) }, pts.Select(p => (p.Gx, p.Gz)).ToArray());
    }

    [Fact]
    public void AnEmptyRectangleGivesNothing()
    {
        var g = new Ground(5, 5, 0, 0, 2);
        Assert.Empty(Sculpt.Unlocked(g, 2, 2, 1, 1));
    }
}

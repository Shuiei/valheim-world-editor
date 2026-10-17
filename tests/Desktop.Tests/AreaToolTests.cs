using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.AreaToolTestScene;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area tool's selection: a box or polygon selection, its grid box, the area it covers, the soft edge, and the zones under it.
/// </summary>
public class AreaToolTests
{
    [Fact]
    public void ABoxWithoutASoftEdgeHasStraightEdges()
    {
        var a = Box(10, 10, 20, 20);
        var w = a.WeightsIn(129, 129)!;
        var at = w.Cells.ToDictionary(c => c.G, c => c.W);
        Assert.Equal(1, at[15 * 129 + 15], 3);
        // On the outline: half.
        Assert.Equal(0.5f, at[15 * 129 + 10], 3);
        Assert.False(at.ContainsKey(15 * 129 + 9));
        Assert.Equal(100, AreaTool.Area(a.Polygon()!), 3);
    }

    [Fact]
    public void TheGridBoxIsTheWholePointsRoundTheOutlineInTheBlock()
    {
        Assert.Equal((10, 10, 20, 21), Box(10.4f, 10, 20, 20.6f).GridBox(129, 129));
        Assert.Equal((0, 0, 128, 10), Box(-5, -5, 200, 10).GridBox(129, 129));
        Assert.Null(new AreaTool().GridBox(129, 129));
    }

    [Fact]
    public void ACrossedOutlinesAreaIsWhatTheSelectionPicks()
    {
        // The shoelace formula let crossed parts cancel: a figure eight read 0 m² with both loops selected.
        List<Vector2> eight = [new(0, 0), new(10, 10), new(10, 0), new(0, 10)];
        Assert.Equal(50, AreaTool.Area(eight), 3);
        // A crossed star: its middle is not selected, nor counted.
        List<Vector2> star = [.. Enumerable.Range(0, 5).Select(i => i * 2 % 5 * MathF.Tau / 5).Select(t => new Vector2(20 + 10 * MathF.Cos(t), 20 + 10 * MathF.Sin(t)))];
        int picked = 0;
        for (int i = 0; i < 600; i++)
        {
            for (int j = 0; j < 600; j++)
            {
                picked += AreaTool.Inside(star, 5 + (j + 0.5f) * 0.05f, 5 + (i + 0.5f) * 0.05f) ? 1 : 0;
            }
        }
        Assert.Equal(picked * 0.0025f, AreaTool.Area(star), 1f);
        // The shoelace formula gave about 147 (the middle counted twice); the five points are about 77.7.
        Assert.InRange(AreaTool.Area(star), 76f, 79f);
    }

    [Fact]
    public void TheSoftEdgeFadesInwards()
    {
        var a = Box(10, 10, 30, 30, soft: 4);
        var at = a.WeightsIn(129, 129)!.Cells.ToDictionary(c => c.G, c => c.W);
        Assert.Equal(1, at[20 * 129 + 20], 3);
        Assert.True(at[20 * 129 + 12] is > 0 and < 1);
        Assert.False(at.ContainsKey(20 * 129 + 10));
    }

    [Fact]
    public void APolygonClosesOnADoubleClick()
    {
        var a = new AreaTool { Box = false };
        a.Down(new Vector2(0, 0), 1);
        a.Down(new Vector2(10, 0), 1);
        Assert.Null(a.Polygon());
        a.Down(new Vector2(10, 10), 1);
        Assert.NotNull(a.Polygon());
        Assert.False(a.Closed);
        a.Down(new Vector2(10, 10), 2);
        Assert.True(a.Closed);
        Assert.Equal(3, a.Points.Count);
        // A click after closing starts a new one.
        a.Down(new Vector2(50, 50), 1);
        Assert.Single(a.Points);
    }

    // The zones under a selection do not depend on the Soft slider: a selection narrower than the soft edge (every
    // point's weight well under one half) still names its zones, and one that crosses a zone border names both sides.
    [Fact]
    public void ZonesUnderDoNotDependOnTheSoftEdge()
    {
        var s = TestScenes.Flat(2);
        Assert.Equal([(0, 0)], Box(40, 40, 55, 55, soft: 20).ZonesUnder(s.Ground));
        Assert.Equal(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }.OrderBy(z => z), Box(56, 56, 72, 72, soft: 20).ZonesUnder(s.Ground).OrderBy(z => z));
        Assert.Equal([(0, 0)], Box(40, 40, 55, 55).ZonesUnder(s.Ground));
        // A grid point on the zones' shared edge belongs to both.
        Assert.Equal(new[] { (0, 0), (1, 0) }.OrderBy(z => z), Box(63.5f, 10.5f, 64.5f, 11.5f, soft: 20).ZonesUnder(s.Ground).OrderBy(z => z));
    }
}

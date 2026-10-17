using System.Numerics;
using Xunit;

using static TerrainEditor.Desktop.Tests.SharedPathTool;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path tool's shapes on the ground: the curve through the points, flattening, smoothing, ramps, rivers and painting
/// along the line, and the natural path that is ragged but still there.
/// </summary>
public class PathToolTests
{
    [Fact]
    public void TheCurveGoesThroughThePointsAboutAMetreApart()
    {
        var p = Line((10, 10), (30, 10), (30, 30));
        var c = p.Curve();
        Assert.Equal(new Vector2(10, 10), c[0].P);
        Assert.Equal(new Vector2(30, 30), c[^1].P);
        Assert.Contains(c, q => Vector2.Distance(q.P, new Vector2(30, 10)) < 1e-4f);
        Assert.InRange(c.Count, 40, 42);
        // Smooth: it bulges past the corner; straight: exactly 40 m.
        Assert.True(PathTool.Length(c) > 40);
        p.Curved = false;
        Assert.Equal(40, PathTool.Length(p.Curve()), 3);
    }

    [Fact]
    public void FlattenLevelsTheWidthAndBlendsTheSoftEdge()
    {
        var s = TestScenes.Flat(2);
        var p = Line((20, 64), (100, 64));
        p.Height = 34;
        p.Width = 8;
        p.Soft = 4;
        Apply(s, p);
        Assert.Equal(34, H(s, 60, 64), 3);
        Assert.Equal(34, H(s, 60, 68), 3);
        float edge = H(s, 60, 70);
        Assert.True(edge is > 30 and < 34, $"{edge}");
        Assert.Equal(30, H(s, 60, 73), 3);
        Assert.Equal("Path", s.UndoLabel);
    }

    // Smoothing along the area's west edge only looks at ground there, not at the east edge (the 5 × 5
    // kernel wrapped into the row before, at the far side of the area).
    [Fact]
    public void SmoothingAtTheEdgeDoesNotWrapAround()
    {
        var s = TestScenes.Flat(2);
        int w = s.Ground.W, hgt = s.Ground.H;
        s.EditGround("east up", g =>
        {
            var touched = new List<int>();
            for (int z = 0; z < hgt; z++)
            {
                for (int x = w - 2; x < w; x++)
                {
                    g.SetHeight(z * w + x, 38);
                    touched.Add(z * w + x);
                }
            }
            return (touched, (w - 2, 0, w - 1, hgt - 1));
        });
        var p = Line((1, 10), (1, 100));
        p.Act = PathTool.Action.Smooth;
        p.Width = 2;
        p.Soft = 0;
        Apply(s, p);
        Assert.Equal(30, H(s, 1, 60), 3);
        Assert.Equal(30, H(s, 0, 60), 3);
    }

    [Fact]
    public void ARampGoesFromStartToEnd()
    {
        var s = TestScenes.Flat(2);
        var p = Line((20, 64), (100, 64));
        p.Act = PathTool.Action.Ramp;
        p.Start = 30;
        p.End = 36;
        p.Soft = 0;
        Apply(s, p);
        Assert.Equal(30, H(s, 20, 64), 2);
        Assert.Equal(33, H(s, 60, 64), 2);
        Assert.Equal(36, H(s, 100, 64), 2);
    }

    [Fact]
    public void ARiverDigsBelowTheSeaAndNeverRaises()
    {
        var s = TestScenes.Flat(2);
        var p = Line((20, 64), (100, 64));
        p.Act = PathTool.Action.River;
        p.Depth = 2;
        Apply(s, p);
        // Sea level is 30 here: the middle of the bed is 2 m below it.
        Assert.Equal(s.Scene.Water - 2, H(s, 60, 64), 2);
        Assert.True(H(s, 60, 66) <= 30);
        Assert.Equal(30, H(s, 60, 80), 3);
    }

    [Fact]
    public void PaintingFollowsTheLine()
    {
        var s = TestScenes.Flat(2);
        var p = Line((20, 64), (100, 64));
        p.Act = PathTool.Action.PaintPaved;
        Apply(s, p);
        int g = 64 * s.Ground.W + 60;
        Assert.Equal(1, s.Ground.PMod[g]);
        Assert.Equal(1, s.Ground.Paint[g * 4 + 2], 3);
        Assert.Equal(0, s.Ground.PMod[80 * s.Ground.W + 60]);
    }

    [Fact]
    public void ANaturalPathIsRaggedButStillThere()
    {
        var s = TestScenes.Flat(2);
        s.Brush.Noise = new Noise(7);
        var p = Line((20, 64), (100, 64));
        p.Act = PathTool.Action.Raise;
        p.Natural = true;
        Apply(s, p);
        var middle = Enumerable.Range(30, 60).Select(x => H(s, x, 64)).ToList();
        Assert.All(middle, h => Assert.True(h > 30));
        Assert.True(middle.Max() - middle.Min() > 0.1f, "bumps");
    }
}

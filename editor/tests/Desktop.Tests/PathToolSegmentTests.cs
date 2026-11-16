using System.Numerics;
using Xunit;

using static TerrainEditor.Desktop.Tests.SharedPathTool;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path tool's segments and trench: the nearest segment to a point, the cave dug along the line, and the things
/// kept out of the trench except near its deep points.
/// </summary>
public class PathToolSegmentTests
{
    [Fact]
    public void TheNearestSegmentIsWhatTestingEverySegmentGives()
    {
        // A winding line with a sharp turn and a repeated point (a zero-length segment).
        var p = Line((10, 10), (40, 12), (42, 40), (42, 40), (12, 30), (30, 20));
        var c = p.Curve();
        const int X0 = 0, Z0 = 0, X1 = 55, Z1 = 50;
        const float Reach = 6.5f;
        var near = PathTool.NearestOnLine(c, X0, Z0, X1, Z1, Reach);
        int w = X1 - X0 + 1, within = 0;
        for (int gz = Z0; gz <= Z1; gz++)
        {
            for (int gx = X0; gx <= X1; gx++)
            {
                float best = float.MaxValue, bestT = 0;
                int seg = 0;
                for (int i = 1; i < c.Count; i++)
                {
                    Vector2 a = c[i - 1].P, d = c[i].P - a;
                    float l2 = MathF.Max(d.LengthSquared(), 1e-9f);
                    float t = Math.Clamp(((gx - a.X) * d.X + (gz - a.Y) * d.Y) / l2, 0, 1);
                    float dist = Vector2.Distance(new Vector2(gx, gz), a + d * t);
                    if (dist < best)
                    {
                        (best, seg, bestT) = (dist, i, t);
                    }
                }
                int k = (gz - Z0) * w + (gx - X0);
                if (best > Reach)
                {
                    Assert.Equal(0, near.Seg[k]);
                    continue;
                }
                within++;
                Assert.Equal(best, near.Dist[k]);
                Assert.Equal(seg, near.Seg[k]);
                Assert.Equal(bestT, near.T[k]);
            }
        }
        Assert.True(within > 500, $"{within}");
    }

    [Fact]
    public void ASegmentRunsFromItsStartToTheNextPoint()
    {
        var curve = new List<(Vector2 P, int Seg)> { (new Vector2(0, 0), 0), (new Vector2(3, 4), 1) };
        var (a, d, l2) = PathTool.Segment(curve, 1);
        Assert.Equal(new Vector2(0, 0), a);
        Assert.Equal(new Vector2(3, 4), d);
        Assert.Equal(25f, l2, 3);
    }

    [Fact]
    public void APointSegmentStillHasALengthSquaredToProjectOnto()
    {
        var curve = new List<(Vector2 P, int Seg)> { (new Vector2(2, 2), 0), (new Vector2(2, 2), 1) };
        var (_, _, l2) = PathTool.Segment(curve, 1);
        Assert.Equal(1e-9f, l2, 12);
    }
}

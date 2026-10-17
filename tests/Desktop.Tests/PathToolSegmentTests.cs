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
    public void ACaveIsDugAlongTheLineItWasPlannedOn()
    {
        var s = TestScenes.Flat(2);
        var p = Line((20, 64), (60, 64));
        p.Act = PathTool.Action.Cave;
        p.Width = 4;
        var cave = p.PlanCave(s.Ground);
        // The line grows after the plan: the plan's line is dug, not the new one.
        p.Points.Add(new Vector2(110, 64));
        var (touched, _, _) = p.Apply(s.Ground, s.Brush, s.Scene.Water, null, cave);
        Assert.NotEmpty(touched);
        Assert.DoesNotContain(touched, i => i % s.Ground.W > 70);
    }

    [Fact]
    public void ThingsAreInTheTrenchOnlyNearItsDeepPoints()
    {
        List<(Vector2 P, int Seg)> curve = [.. Enumerable.Range(0, 41).Select(i => (new Vector2(10 + i, 20), 0))];
        float[] depth = [.. Enumerable.Range(0, 41).Select(i => i is < 5 or > 35 ? 0.2f : 2f)];
        var cave = new PathTool.CavePlan(curve, new float[41], depth, new float[41]);
        var inTrench = PathTool.NearDeep(cave, 3);
        Assert.True(inTrench(new Vector2(30, 22.9f)));
        Assert.True(inTrench(new Vector2(12.5f, 20)), "within reach of a deep point");
        Assert.False(inTrench(new Vector2(30, 23.1f)), "beyond the reach");
        Assert.False(inTrench(new Vector2(10, 20)), "only shallow points near");
        Assert.False(inTrench(new Vector2(48, 21)), "past the end");
        Assert.False(inTrench(new Vector2(-300, 900)), "far away");
    }
}

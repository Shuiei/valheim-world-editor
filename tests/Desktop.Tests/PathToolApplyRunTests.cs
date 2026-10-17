using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path tool's apply steps one at a time on a flat ground: the extents the line reaches, each action's change to one
/// grid point, the Smooth and paint reads taken before the change, and the noise the bumps come from.
/// </summary>
public class PathToolApplyRunTests
{
    /// <summary>A path of the given action over the points, 8 m wide with a 4 m soft edge.</summary>
    private static PathTool Line(PathTool.Action act, params (float X, float Z)[] pts)
    {
        var tool = new PathTool { Act = act, Width = 8, Soft = 4 };
        foreach (var (x, z) in pts)
        {
            tool.Points.Add(new Vector2(x, z));
        }
        return tool;
    }

    /// <summary>Runs a body on the ground of the scene's area, as an edit.</summary>
    private static void OnGround(EditSession s, Action<Ground> body) =>
        s.EditGround("test", g =>
        {
            body(g);
            return (new List<int>(), (0, 0, 0, 0));
        });

    [Fact]
    public void PrepareReachesTheWidthAndTheSoftEdgeBeyondTheLine()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Raise, (10, 10), (20, 10));
        (int X0, int Z0, int X1, int Z1) rect = default;
        s.EditGround("test", g =>
        {
            // Half the width (4) plus the soft edge (4) plus one, around the line from x 10 to 20 at z 10.
            var run = new PathTool.ApplyRun(tool, g, s.Brush, s.Scene.Water, null, null, tool.Curve());
            run.Prepare();
            rect = run.Run().Rect;
            return (new List<int>(), rect);
        });
        Assert.Equal((0, 0, 30, 20), rect);
    }

    [Fact]
    public void RaiseAddsTheAmountAndTheBumpTimesTheWeight()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Raise, (10, 10), (20, 10));
        tool.Amount = 2;
        OnGround(s, g =>
        {
            int p = 10 * g.W + 15;
            float h = g.HeightOf(p);
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            run.Prepare();
            run.ActOn(new PathTool.ApplyRun.PointSample(15, 10, p, h, 0.5f, 0, 0.25f, 0, 0, 0));
            Assert.Equal(h + (2 + 0.25f) * 0.5f, g.HeightOf(p), 2);
        });
    }

    [Fact]
    public void LowerTakesAwayTheAmount()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Lower, (10, 10), (20, 10));
        tool.Amount = 2;
        OnGround(s, g =>
        {
            int p = 10 * g.W + 15;
            float h = g.HeightOf(p);
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            run.Prepare();
            run.ActOn(new PathTool.ApplyRun.PointSample(15, 10, p, h, 1f, 0, 0, 0, 0, 0));
            Assert.Equal(h - 2, g.HeightOf(p), 2);
        });
    }

    [Fact]
    public void FlattenMovesThePointToTheHeightOnTheWeight()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Flatten, (10, 10), (20, 10));
        tool.Height = 30;
        OnGround(s, g =>
        {
            int p = 10 * g.W + 15;
            float h = g.HeightOf(p);
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            run.Prepare();
            run.ActOn(new PathTool.ApplyRun.PointSample(15, 10, p, h, 1f, 0, 0, 0, 0, 0));
            Assert.Equal(30f, g.HeightOf(p), 2);
        });
    }

    [Fact]
    public void ARiverBedOnlyDigs()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.River, (10, 10), (20, 10));
        tool.Depth = 2;
        OnGround(s, g =>
        {
            int p = 10 * g.W + 15;
            float h = g.HeightOf(p);
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 30, null, null, tool.Curve());
            run.Prepare();
            // In the middle of the bed (no distance): the water less the depth, 28 m.
            run.DigRiver(new PathTool.ApplyRun.PointSample(15, 10, p, h, 1f, 0, 0, 0, 0, 4));
            Assert.Equal(28f, g.HeightOf(p), 2);
            // A point already below the bed is left alone.
            int q = 11 * g.W + 15;
            float low = g.HeightOf(q);
            run.DigRiver(new PathTool.ApplyRun.PointSample(15, 11, q, 20f, 1f, 0, 0, 0, 0, 4));
            Assert.Equal(low, g.HeightOf(q), 2);
        });
    }

    [Fact]
    public void ACaveFloorIsNotDugBelowTheGamesLimitUnlessNoLimitIsOn()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Cave, (10, 10), (20, 10));
        OnGround(s, g =>
        {
            int p = 10 * g.W + 15;
            float h = g.HeightOf(p);
            float limit = g.Original(p) - Editing.EditStore.MaxLevel + 0.1f;
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            run.Prepare();
            // The floor asked for is 20 m, but the game's limit (8 m below the ground) is higher, so the limit is the floor.
            run.DigCave(new PathTool.ApplyRun.PointSample(15, 10, p, h, 1f, 0, 0, 20f, 0, 0));
            Assert.Equal(Math.Max(20f, limit), g.HeightOf(p), 2);
        });
    }

    [Fact]
    public void ACaveFloorWithNoLimitGoesStraightToItsFloor()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Cave, (10, 10), (20, 10));
        OnGround(s, g =>
        {
            g.NoLimit = true;
            int p = 10 * g.W + 15;
            float h = g.HeightOf(p);
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            run.Prepare();
            run.DigCave(new PathTool.ApplyRun.PointSample(15, 10, p, h, 1f, 0, 0, 20f, 0, 0));
            Assert.Equal(20f, g.HeightOf(p), 2);
        });
    }

    [Fact]
    public void SmoothAveragesTheFiveByFiveAroundThePointBeforeTheChange()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Smooth, (10, 10), (20, 10));
        OnGround(s, g =>
        {
            int centre = 10 * g.W + 10, p = 10 * g.W + 11;
            float flat = g.HeightOf(p);
            g.SetHeight(centre, flat + 5);
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            // The heights are read before the change: the raised centre counts in its neighbours' average.
            run.Prepare();
            run.Smooth(new PathTool.ApplyRun.PointSample(11, 10, p, g.HeightOf(p), 1f, 0, 0, 0, 0, 0));
            Assert.Equal(flat + 5f / 25, g.HeightOf(p), 2);
        });
    }

    [Fact]
    public void PaintWritesTheActionsColourByTheWeight()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.PaintDirt, (10, 10), (20, 10));
        OnGround(s, g =>
        {
            int p = 10 * g.W + 15;
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            run.Prepare();
            run.Paint(new PathTool.ApplyRun.PointSample(15, 10, p, g.HeightOf(p), 1f, 0, 0, 0, 0, 0));
            float[] colour = Brush.PaintOf(BrushTool.PaintDirt)!;
            for (int c = 0; c < 4; c++)
            {
                Assert.Equal(colour[c], g.Paint[p * 4 + c], 4);
            }
        });
    }

    [Fact]
    public void WorldNoiseReadsTheBrushAtTheScaledWorldPoint()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Raise, (10, 10), (20, 10));
        OnGround(s, g =>
        {
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            float expected = s.Brush.Noise.Fbm((g.OriginX + 3) / (s.Brush.NoiseSize * 2), (g.OriginZ + 4) / (s.Brush.NoiseSize * 2));
            Assert.Equal(expected, run.WorldNoise(3, 4, 2), 5);
        });
    }

    [Fact]
    public void APointTooFarFromTheLineIsLeftAlone()
    {
        var s = TestScenes.Flat(2);
        var tool = Line(PathTool.Action.Raise, (10, 10), (20, 10));
        tool.Amount = 2;
        OnGround(s, g =>
        {
            var run = new PathTool.ApplyRun(tool, g, s.Brush, 0, null, null, tool.Curve());
            run.Prepare();
            // The extents of the run, as Prepare works them out: x 1..29, z 1..19.
            var near = PathTool.NearestOnLine(tool.Curve(), 1, 1, 29, 19, 9);
            // Nine metres from the line (its reach): past the half width and the soft edge, so untouched.
            int far = 19 * g.W + 15;
            float before = g.HeightOf(far);
            run.Point(15, 19, near, 29);
            Assert.Equal(before, g.HeightOf(far), 2);
            // A point on the line is raised.
            int on = 10 * g.W + 15;
            float onBefore = g.HeightOf(on);
            run.Point(15, 10, near, 29);
            Assert.Equal(onBefore + 2, g.HeightOf(on), 2);
        });
    }
}

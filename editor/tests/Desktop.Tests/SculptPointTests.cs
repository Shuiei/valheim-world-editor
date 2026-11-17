using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a sculpt or paint frame does to one grid point: the raise and lower, the restore of edits, and the paint.
/// </summary>
public class SculptPointTests
{
    private static readonly float[] Expected2 = [0f, 1f, 0f, 1f];
    private static readonly float[] Expected1 = [1f, 0f, 0f, 1f];
    /// <summary>
    /// A 5 × 5 ground and its middle point (2, 2).
    /// </summary>
    private static (Ground G, int P) Point() => (new Ground(5, 5, 0, 0, 2), 2 * 5 + 2);

    /// <summary>
    /// A stroke of this tool, started from the ground as it is.
    /// </summary>
    private static Stroke StrokeOf(Ground g, BrushTool tool) => new() { Tool = tool, Start = g.Snapshot() };

    [Fact]
    public void RaiseLiftsThePointBySixTimesTheRateAndWeight()
    {
        var (g, p) = Point();
        float before = g.HeightOf(p);
        Sculpt.ApplyPoint(g, new Brush(), StrokeOf(g, BrushTool.Raise), new Sculpt.Frame(0.1f, null, null), p, 0.5f);
        Assert.InRange(g.HeightOf(p) - before, 0.299f, 0.301f);
    }

    [Fact]
    public void LowerDigsThePointBySixTimesTheRateAndWeight()
    {
        var (g, p) = Point();
        float before = g.HeightOf(p);
        Sculpt.ApplyPoint(g, new Brush(), StrokeOf(g, BrushTool.Lower), new Sculpt.Frame(0.1f, null, null), p, 0.5f);
        Assert.InRange(before - g.HeightOf(p), 0.299f, 0.301f);
    }

    [Fact]
    public void RestoreTakesAnEditBackToTheGround()
    {
        var (g, p) = Point();
        g.Mod[p] = 1;
        g.Level[p] = 0.5f;
        g.Smooth[p] = 0;
        GroundOps.Restore(g, p, 1, 1);
        Assert.Equal((byte)0, g.Mod[p]);
        Assert.Equal(0f, g.Level[p]);
    }

    [Fact]
    public void RestoreClearsAPaintedPointOnlyWhereTheBrushIsStrong()
    {
        var (g, p) = Point();
        g.PMod[p] = 1;
        GroundOps.Restore(g, p, 1, 0.4f);
        Assert.Equal((byte)1, g.PMod[p]);
        GroundOps.Restore(g, p, 1, 0.6f);
        Assert.Equal((byte)0, g.PMod[p]);
    }

    [Fact]
    public void ARestoreStrokeTakesAShareOfTheEditBackEachFrame()
    {
        var (g, p) = Point();
        g.SetHeight(p, g.HeightOf(p) + 4);
        float lifted = g.HeightOf(p), ground = g.Original(p);
        // A frame of 0.05 at full weight: 8 × 0.05 = 0.4 of the way back.
        Sculpt.ApplyPoint(g, new Brush(), StrokeOf(g, BrushTool.Restore), new Sculpt.Frame(0.05f, null, null), p, 1f);
        Assert.Equal(ground + (lifted - ground) * 0.6f, g.HeightOf(p), 3);
    }

    [Fact]
    public void ASmoothStrokePullsThePointTowardsTheAverageOfItsNineNeighbours()
    {
        var (g, p) = Point();
        float flat = g.HeightOf(p);
        g.SetHeight(p, flat + 3);
        var before = HeightsBefore.Take(g, 2, 2, 2, 2, GroundOps.BrushSmoothReach);
        // A frame of 0.1 at full weight: 10 × 0.1 = all the way to the average, a ninth of the bump.
        Sculpt.ApplyPoint(g, new Brush(), StrokeOf(g, BrushTool.Smooth), new Sculpt.Frame(0.1f, before, null), p, 1f);
        Assert.Equal(flat + 3f / 9, g.HeightOf(p), 3);
    }

    [Fact]
    public void PaintPointMovesAllFourChannelsTowardsTheColourByTheStrength()
    {
        var (g, p) = Point();
        GroundOps.Paint(g, p, [0f, 1f, 0f, 1f], 1f);
        Assert.Equal(Expected2, g.Paint.Skip(p * 4).Take(4).ToArray());
    }

    [Fact]
    public void PaintingAPointTakesItsPaintTowardsTheToolsColour()
    {
        var (g, p) = Point();
        Sculpt.ApplyPoint(g, new Brush(), StrokeOf(g, BrushTool.PaintDirt), new Sculpt.Frame(0.2f, null, [1f, 0f, 0f, 1f]), p, 1f);
        Assert.Equal(Expected1, g.Paint.Skip(p * 4).Take(4).ToArray());
    }
}

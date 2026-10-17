using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a sculpt or paint frame does to one grid point: the raise and lower, the restore of edits, and the paint.
/// </summary>
public class SculptPointTests
{
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
        Sculpt.ApplyPoint(g, new Brush(), StrokeOf(g, BrushTool.Raise), new Sculpt.Frame(0.1f, null, 0, 0, 0, null), 2, 2, p, 0.5f);
        Assert.InRange(g.HeightOf(p) - before, 0.299f, 0.301f);
    }

    [Fact]
    public void LowerDigsThePointBySixTimesTheRateAndWeight()
    {
        var (g, p) = Point();
        float before = g.HeightOf(p);
        Sculpt.ApplyPoint(g, new Brush(), StrokeOf(g, BrushTool.Lower), new Sculpt.Frame(0.1f, null, 0, 0, 0, null), 2, 2, p, 0.5f);
        Assert.InRange(before - g.HeightOf(p), 0.299f, 0.301f);
    }

    [Fact]
    public void RestoreTakesAnEditBackToTheGround()
    {
        var (g, p) = Point();
        g.Mod[p] = 1;
        g.Level[p] = 0.5f;
        g.Smooth[p] = 0;
        Sculpt.RestorePoint(g, p, 1, 1);
        Assert.Equal((byte)0, g.Mod[p]);
        Assert.Equal(0f, g.Level[p]);
    }

    [Fact]
    public void RestoreClearsAPaintedPointOnlyWhereTheBrushIsStrong()
    {
        var (g, p) = Point();
        g.PMod[p] = 1;
        Sculpt.RestorePoint(g, p, 1, 0.4f);
        Assert.Equal((byte)1, g.PMod[p]);
        Sculpt.RestorePoint(g, p, 1, 0.6f);
        Assert.Equal((byte)0, g.PMod[p]);
    }

    [Fact]
    public void PaintPointMovesAllFourChannelsTowardsTheColourByTheStrength()
    {
        var (g, p) = Point();
        Sculpt.PaintPoint(g, p, [0f, 1f, 0f, 1f], 1f);
        Assert.Equal(new[] { 0f, 1f, 0f, 1f }, g.Paint.Skip(p * 4).Take(4).ToArray());
    }

    [Fact]
    public void PaintingAPointTakesItsPaintTowardsTheToolsColour()
    {
        var (g, p) = Point();
        Sculpt.ApplyPoint(g, new Brush(), StrokeOf(g, BrushTool.PaintDirt), new Sculpt.Frame(0.2f, null, 0, 0, 0, [1f, 0f, 0f, 1f]), 2, 2, p, 1f);
        Assert.Equal(new[] { 1f, 0f, 0f, 1f }, g.Paint.Skip(p * 4).Take(4).ToArray());
    }
}

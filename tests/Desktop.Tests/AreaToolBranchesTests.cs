using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.AreaToolTestScene;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area tool's less travelled paths: the mouse on and off the ground, boxes too small to keep, closing and
/// shortening a polygon, every action's name, the actions and volumes with no selection, at the block's edge, on
/// painted and edited ground, and with a soft edge.
/// </summary>
public class AreaToolBranchesTests
{
    /// <summary>
    /// A flat 2 × 2 zone ground at 30 m.
    /// </summary>
    private static Ground Flat()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        Array.Fill(g.Base, 30f);
        return g;
    }

    [Fact]
    public void ABoxIsDraggedAndATinyOneDropped()
    {
        var a = new AreaTool();
        int changed = 0;
        a.Changed += () => changed++;
        // Off the ground, nothing; a release without a drag, nothing; a move without a drag, nothing.
        a.Down(null, 1);
        a.Up();
        a.Moved(new Vector2(5, 5));
        Assert.Empty(a.Points);
        Assert.Equal(0, changed);
        a.Down(new Vector2(10, 10), 1);
        a.Moved(null);
        a.Moved(new Vector2(30, 20));
        a.Up();
        Assert.Equal(new Vector2(30, 20), a.Points[1]);
        Assert.NotNull(a.Polygon());
        // Smaller than a metre: dropped.
        a.Down(new Vector2(10, 10), 1);
        a.Moved(new Vector2(10.5f, 10.2f));
        a.Up();
        Assert.Empty(a.Points);
        Assert.Null(a.Polygon());
        // Long but less than a metre wide (a line): dropped too.
        a.Down(new Vector2(10, 10), 1);
        a.Moved(new Vector2(40, 10.5f));
        a.Up();
        Assert.Empty(a.Points);
        // A box has no polygon to close or shorten.
        a.Points.Add(new Vector2(1, 1));
        a.Points.Add(new Vector2(4, 4));
        Assert.False(a.CloseWithEnter());
        a.RemoveLast();
        Assert.Equal(2, a.Points.Count);
    }

    [Fact]
    public void APolygonIsClosedWithEnterAndShortenedWithBackspace()
    {
        var a = new AreaTool { Box = false };
        a.RemoveLast();
        Assert.Empty(a.Points);
        a.Down(new Vector2(0, 0), 1);
        a.Down(new Vector2(10, 0), 1);
        // Too few points to close.
        Assert.False(a.CloseWithEnter());
        a.Down(new Vector2(10, 10), 1);
        a.RemoveLast();
        Assert.Equal(2, a.Points.Count);
        Assert.Null(a.Polygon());
        a.Down(new Vector2(10, 10), 1);
        Assert.True(a.CloseWithEnter());
        Assert.True(a.Closed);
        // Closed already: Enter applies instead.
        Assert.False(a.CloseWithEnter());
        Assert.Equal(50, AreaTool.Area(a.Polygon()!), 3);
    }

    [Theory]
    [InlineData(AreaTool.GroundAction.Flatten, "Flatten")]
    [InlineData(AreaTool.GroundAction.Raise, "Raise")]
    [InlineData(AreaTool.GroundAction.Lower, "Lower")]
    [InlineData(AreaTool.GroundAction.Smooth, "Smooth")]
    [InlineData(AreaTool.GroundAction.Natural, "Naturalize")]
    [InlineData(AreaTool.GroundAction.Restore, "Restore the ground")]
    [InlineData(AreaTool.GroundAction.Erode, "Erode")]
    [InlineData(AreaTool.GroundAction.Paint, "Paint")]
    public void EachActionHasItsName(AreaTool.GroundAction act, string label) => Assert.Equal(label, AreaTool.Label(act));

    [Fact]
    public void WithoutASelectionNothingIsDone()
    {
        var g = Flat();
        var a = new AreaTool();
        var (touched, rect, _) = a.Apply(g, new Brush(), AreaTool.GroundAction.Raise, 0, 2, new float[4]);
        Assert.Empty(touched);
        Assert.Equal((0, 0, 0, 0), rect);
        Assert.Equal("", a.Volume(g, AreaTool.GroundAction.Flatten, 30, 2));
        Assert.Null(a.Average(g));
        Assert.Empty(a.ZonesUnder(g));
        Assert.Null(a.WeightsIn(129, 129));
    }

    [Fact]
    public void TheBlocksEdgeIsLeftAloneAndSmoothingStaysOnTheGround()
    {
        var g = Flat();
        // A bump by the corner, then smoothed and naturalized from the very edge.
        g.Mod[3 * 129 + 3] = 1;
        g.Level[3 * 129 + 3] = 4;
        var a = Box(0, 0, 10, 10);
        var (touched, _, _) = a.Apply(g, new Brush(), AreaTool.GroundAction.Smooth, 0, 0, new float[4]);
        Assert.DoesNotContain(0, touched);
        Assert.Contains(129 + 1, touched);
        Assert.True(g.HeightOf(3 * 129 + 3) < 34);
        var (natural, _, _) = a.Apply(g, new Brush(), AreaTool.GroundAction.Natural, 0, 0, new float[4]);
        Assert.NotEmpty(natural);
        var (eroded, _, _) = Box(20, 20, 40, 40).Apply(g, new Brush(), AreaTool.GroundAction.Erode, 0, 0, new float[4]);
        Assert.NotNull(eroded);
    }

    // Each action says when a point reached the game's ±8 m limit; Erode dropped it.
    [Fact]
    public void PointsReachingTheLimitAreSaid()
    {
        var g = Flat();
        var a = Box(10, 10, 30, 30);
        Assert.False(a.Apply(g, new Brush(), AreaTool.GroundAction.Raise, 0, 2, new float[4]).Clamped);
        Assert.True(a.Apply(g, new Brush(), AreaTool.GroundAction.Raise, 0, 10, new float[4]).Clamped);
        // A slope already dug the full 8 m down: eroding it digs further.
        var slope = Flat();
        for (int p = 0; p < slope.W * slope.H; p++)
        {
            slope.Base[p] = 30 + p % slope.W;
            slope.Mod[p] = 1;
            slope.Level[p] = -8;
        }
        Assert.True(Box(20, 20, 40, 40).Apply(slope, new Brush(), AreaTool.GroundAction.Erode, 0, 0, new float[4]).Clamped);
    }

    [Fact]
    public void RestoreUndoesEditsAndPaintFadingAtTheSoftEdge()
    {
        var g = Flat();
        for (int p = 0; p < g.W * g.H; p++)
        {
            g.Mod[p] = 1;
            g.Level[p] = 6;
            g.Lift[p] = 5;
            g.PMod[p] = 1;
        }
        var a = Box(20, 20, 60, 60, soft: 10);
        a.Apply(g, new Brush(), AreaTool.GroundAction.Restore, 0, 0, new float[4]);
        // The middle: all gone.
        int mid = 40 * 129 + 40;
        Assert.Equal(0, g.Mod[mid]);
        Assert.Equal(0, g.Lift[mid]);
        Assert.Equal(0, g.PMod[mid]);
        // Just inside the outline: only part of the way back, the paint kept.
        int edge = 40 * 129 + 21;
        Assert.Equal(1, g.Mod[edge]);
        Assert.InRange(g.Level[edge], 0.01f, 5.99f);
        Assert.InRange(g.Lift[edge], 0.01f, 4.99f);
        Assert.Equal(1, g.PMod[edge]);
    }

    [Fact]
    public void RestoreOnGroundEditedOnlyALittleClearsItAtTheSoftEdgeToo()
    {
        var g = Flat();
        int edge = 40 * 129 + 21;
        g.Mod[edge] = 1;
        g.Level[edge] = 0.001f;
        var a = Box(20, 20, 60, 60, soft: 10);
        a.Apply(g, new Brush(), AreaTool.GroundAction.Restore, 0, 0, new float[4]);
        Assert.Equal(0, g.Mod[edge]);
        Assert.Equal(0, g.Level[edge]);
    }

    [Fact]
    public void PaintMixesIntoPaintAlreadyThere()
    {
        var g = Flat();
        float[] dirt = Brush.PaintOf(BrushTool.PaintDirt)!, paved = Brush.PaintOf(BrushTool.PaintPaved)!;
        int painted = 30 * 129 + 30, bare = 30 * 129 + 35;
        g.PMod[painted] = 1;
        Array.Copy(dirt, 0, g.Paint, painted * 4, 4);
        Box(20, 20, 40, 40).Apply(g, new Brush(), AreaTool.GroundAction.Paint, 0, 0, paved);
        Assert.Equal(paved, g.Paint[(painted * 4)..(painted * 4 + 4)]);
        Assert.Equal(paved, g.Paint[(bare * 4)..(bare * 4 + 4)]);
        Assert.Equal(1, g.PMod[bare]);
    }

    [Fact]
    public void TheVolumesSayWhatIsOutOfReachAndInThousands()
    {
        var g = Flat();
        var a = Box(1, 1, 120, 120);
        string flatten = a.Volume(g, AreaTool.GroundAction.Flatten, 50, 0);
        Assert.Contains("out of reach: ±8 m limit", flatten);
        Assert.Contains("k m³", flatten);
        Assert.StartsWith("Lower 3 m: dig ", a.Volume(g, AreaTool.GroundAction.Lower, 0, 3));
        Assert.StartsWith("Ground inside: ", a.Volume(g, AreaTool.GroundAction.Smooth, 0, 0));
        // Dug ground counts as dug.
        a.Apply(g, new Brush(), AreaTool.GroundAction.Lower, 0, 2, new float[4]);
        string after = a.Volume(g, AreaTool.GroundAction.Raise, 0, 1);
        Assert.StartsWith("Raise 1 m: fill ", after);
        Assert.Contains("0 m³ raised", after);
        Assert.DoesNotContain(" 0 m³ dug", after);
    }

    [Fact]
    public void TheMaskWeighsTheSelection()
    {
        var a = Box(10, 10, 20, 20);
        var all = a.WeightsIn(129, 129)!;
        var none = a.WeightsIn(129, 129, _ => 0)!;
        Assert.NotEmpty(all.Cells);
        Assert.Empty(none.Cells);
        Assert.Equal((0, 0), Assert.Single(a.ZonesUnder(new Ground(129, 129, 0, 0, 2))));
    }
}

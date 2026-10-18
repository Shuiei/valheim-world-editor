using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.GroundActionsSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Every ground action of the Path tool on a flat area (30 m): what a path does along its width and along itself, the paint it
/// lays, and the points a path needs (a path drawn freehand, dragging a point every two metres).
/// </summary>
public class GroundPathActionTests
{
    /// <summary>
    /// A straight, hard-edged path across the area (grid x 20 to 108 at z 64) doing this action.
    /// </summary>
    private static PathTool Line(PathTool.Action act, float width = 8)
    {
        var p = new PathTool { Act = act, Width = width, Soft = 0, Amount = 2, Curved = false };
        p.Points.Add(new Vector2(20, 64));
        p.Points.Add(new Vector2(108, 64));
        return p;
    }

    /// <summary>
    /// Runs the path as one edit step, through the mask if one is given.
    /// </summary>
    private static List<int> Apply(EditSession s, PathTool p, Func<int, float>? mask = null) =>
        s.EditGround("Path", g => { var (t, r, _) = p.Apply(g, s.Brush, s.Scene.Water, mask); return (t, r); });

    [Theory]
    [InlineData(PathTool.Action.Raise, 32f)]
    [InlineData(PathTool.Action.Lower, 28f)]
    public void APathRaisesAndLowersAlongItsWidth(PathTool.Action act, float expected)
    {
        var s = TestScenes.Flat(2);
        Apply(s, Line(act));
        Assert.Equal(expected, H(s, 64, 64), 2);
        Assert.Equal(expected, H(s, 64, 67), 2);
        Assert.Equal(30, H(s, 64, 72), 3);
        Assert.Equal(30, H(s, 10, 64), 3);
    }

    [Fact]
    public void APathSmoothsAlongItself()
    {
        var s = Bumpy();
        double along = Spread(s, 30, 62, 100, 66), beside = Spread(s, 30, 90, 100, 94);
        Apply(s, Line(PathTool.Action.Smooth));
        Assert.True(Spread(s, 30, 62, 100, 66) < along * 0.8, $"{along:0.000} → {Spread(s, 30, 62, 100, 66):0.000}");
        Assert.Equal(beside, Spread(s, 30, 90, 100, 94), 5);
    }

    [Theory]
    [InlineData(PathTool.Action.PaintDirt, BrushTool.PaintDirt)]
    [InlineData(PathTool.Action.PaintPaved, BrushTool.PaintPaved)]
    [InlineData(PathTool.Action.PaintCultivated, BrushTool.PaintCultivated)]
    public void APathPaintsItsKind(PathTool.Action act, BrushTool kind)
    {
        var s = TestScenes.Flat(2);
        Apply(s, Line(act));
        int p = 64 * s.Ground.W + 64;
        Assert.Equal(1, s.Ground.PMod[p]);
        Assert.Equal(Brush.PaintOf(kind), s.Ground.Paint[(p * 4)..(p * 4 + 4)]);
        Assert.Equal(30, H(s, 64, 64), 3);
    }

    [Fact]
    public void ClearPaintAlongAPathTakesPaintAway()
    {
        var s = TestScenes.Flat(2);
        Apply(s, Line(PathTool.Action.PaintDirt));
        Apply(s, Line(PathTool.Action.PaintClear));
        int p = 64 * s.Ground.W + 64;
        Assert.Equal(Brush.PaintOf(BrushTool.PaintClear), s.Ground.Paint[(p * 4)..(p * 4 + 4)]);
    }

    [Fact]
    public void APathNeedsTwoPointsAndTheMaskStopsIt()
    {
        var s = TestScenes.Flat(2);
        var one = new PathTool { Act = PathTool.Action.Raise };
        one.Points.Add(new Vector2(64, 64));
        Assert.Empty(Apply(s, one));
        Assert.Empty(Apply(s, Line(PathTool.Action.Raise), _ => 0f));
        Assert.Equal(30, H(s, 64, 64), 3);
    }

    [Fact]
    public void APathIsDrawnByDraggingAPointEveryTwoMetres()
    {
        var p = new PathTool();
        var size = new Avalonia.Size(100, 100);
        p.Down(new Vector2(10, 10), new Avalonia.Point(0, 0), _ => null, false, false, false, _ => 30f);
        for (int i = 1; i <= 20; i++)
        {
            p.Moved(new Vector2(10 + i * 0.5f, 10));
        }
        p.Up();
        // 10 m dragged: the first point and one every 2 m.
        Assert.Equal(6, p.Points.Count);
        Assert.Equal(new Vector2(20, 10), p.Points[^1]);
        // After the button is up, moving adds nothing.
        p.Moved(new Vector2(40, 10));
        Assert.Equal(6, p.Points.Count);
    }
}

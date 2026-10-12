using System.Numerics;
using Avalonia;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path tool's remaining branches: a line of one point, the paints and Lower along a line, a repeated point, a
/// mask, Alt picking a height, and drawing by dragging the mouse.
/// </summary>
public class PathToolBranchesTests
{
    /// <summary>
    /// A straight Path tool with these points.
    /// </summary>
    private static PathTool Line(PathTool.Action act, params (float X, float Z)[] pts)
    {
        var p = new PathTool { Act = act, Curved = false, Width = 4, Soft = 2 };
        foreach (var (x, z) in pts)
        {
            p.Points.Add(new Vector2(x, z));
        }
        return p;
    }

    /// <summary>
    /// Off the view: no point of the line is on the screen.
    /// </summary>
    private static Point? Nowhere(Vector2 _) => null;

    [Fact]
    public void ALineOfOnePointDoesNothing()
    {
        var s = TestScenes.Flat(2);
        var (touched, _, clamped) = Line(PathTool.Action.Raise, (20, 20)).Apply(s.Ground, s.Brush, s.Scene.Water);
        Assert.Empty(touched);
        Assert.False(clamped);
    }

    [Theory]
    [InlineData(PathTool.Action.PaintDirt)]
    [InlineData(PathTool.Action.PaintCultivated)]
    [InlineData(PathTool.Action.PaintClear)]
    public void EachPaintPaintsAlongTheLine(PathTool.Action act)
    {
        var s = TestScenes.Flat(2);
        var (touched, _, _) = Line(act, (20, 40), (80, 40)).Apply(s.Ground, s.Brush, s.Scene.Water);
        Assert.NotEmpty(touched);
        Assert.Equal(1, s.Ground.PMod[40 * s.Ground.W + 50]);
        Assert.Equal(Brush.PaintOf(act switch
        {
            PathTool.Action.PaintDirt => BrushTool.PaintDirt,
            PathTool.Action.PaintCultivated => BrushTool.PaintCultivated,
            _ => BrushTool.PaintClear,
        })![1], s.Ground.Paint[(40 * s.Ground.W + 50) * 4 + 1], 3);
    }

    [Fact]
    public void LowerDigsAlongALineWithARepeatedPointUnderAMask()
    {
        var s = TestScenes.Flat(2);
        var p = Line(PathTool.Action.Lower, (20, 40), (20, 40), (80, 40));
        p.Amount = 2;
        var (touched, _, _) = p.Apply(s.Ground, s.Brush, s.Scene.Water, _ => 1f);
        Assert.NotEmpty(touched);
        Assert.Equal(28, s.Ground.HeightOf(40 * s.Ground.W + 50), 2);
    }

    [Fact]
    public void AltPicksTheHeightOrTheRampsStart()
    {
        var p = new PathTool { Act = PathTool.Action.Flatten };
        p.Down(new Vector2(5, 5), new Point(0, 0), Nowhere, ctrl: false, alt: true, shift: false, _ => 41.27f);
        Assert.Equal(41.3f, p.Height, 3);
        p.Act = PathTool.Action.Ramp;
        p.Down(new Vector2(5, 5), new Point(0, 0), Nowhere, ctrl: false, alt: true, shift: false, _ => 12f);
        Assert.Equal(12, p.Start);
        Assert.True(p.RampEdited);
        Assert.Empty(p.Points);
    }

    [Fact]
    public void DraggingDrawsPointsTwoMetresApart()
    {
        var p = new PathTool();
        p.Moved(null);
        p.Moved(new Vector2(1, 1));
        Assert.Empty(p.Points);
        p.Down(new Vector2(10, 10), new Point(0, 0), Nowhere, ctrl: false, alt: false, shift: false, _ => 30);
        Assert.True(p.Drawing);
        p.Moved(null);
        p.Moved(new Vector2(11, 10));
        Assert.Single(p.Points);
        p.Moved(new Vector2(13, 10));
        Assert.Equal(2, p.Points.Count);
        p.Up();
        Assert.False(p.Drawing);
        p.Moved(new Vector2(20, 10));
        Assert.Equal(2, p.Points.Count);
    }
}

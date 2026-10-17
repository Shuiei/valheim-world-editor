using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

using static TerrainEditor.Desktop.Tests.SharedPathTool;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path tool in the 3D editor: clicks make the line and Enter applies it, points are dragged, added on the line and
/// removed with Ctrl, and the ramp follows the ground until a height is typed.
/// </summary>
public class PathToolWindowTests
{
    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area in the Path tool, looking straight down; with the screen
    /// point of a grid point.
    /// </summary>
    private static (MainWindow W, EditSession S, Func<float, float, Avalonia.Point> Screen) Open()
    {
        // Wide, and the line kept near the middle: the tool panels cover the sides.
        var s = TestScenes.Flat(2);
        var w = TestWindows.Editing(s);
        w.KeyPress(Key.P, RawInputModifiers.None, PhysicalKey.P, "p");
        w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
        // Grid point → screen.
        Avalonia.Point Screen(float gx, float gz)
        {
            var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
            return new Avalonia.Point((q.X / q.W + 1) / 2 * 1600, (1 - q.Y / q.W) / 2 * 1000);
        }
        return (w, s, Screen);
    }

    /// <summary>
    /// A left click at this screen point, with these keys held.
    /// </summary>
    private static void Click(MainWindow w, Avalonia.Point at, RawInputModifiers mods = RawInputModifiers.None)
    {
        w.MouseDown(at, MouseButton.Left, mods);
        w.MouseUp(at, MouseButton.Left, mods);
    }

    [AvaloniaFact]
    public void ClicksMakeTheLineAndEnterAppliesIt()
    {
        var (w, s, screen) = Open();
        Assert.Equal(ToolMode.Path, w.View.Mode);
        Assert.True(w.PathPanel.Card.IsVisible);
        Click(w, screen(40, 64));
        Click(w, screen(64, 64));
        Click(w, screen(88, 64));
        Assert.Equal(3, w.View.Path.Points.Count);
        Assert.Equal(64, w.View.Path.Points[1].X, 0);
        Assert.Equal("3 point(s), 48 m long.", w.PathPanel.Info.Text);
        w.PathPanel.HeightBox.Value = 33;
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        Assert.Equal(33, H(s, 64, 64), 2);
        Assert.StartsWith("Applied", w.MessageText.Text);
        // The line is kept; Backspace takes off the last point, Esc clears it.
        w.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Assert.Equal(2, w.View.Path.Points.Count);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Empty(w.View.Path.Points);
        Assert.Equal(ToolMode.Path, w.View.Mode);
    }

    [AvaloniaFact]
    public void PointsAreDraggedAddedOnTheLineAndRemovedWithCtrl()
    {
        var (w, _, screen) = Open();
        var p = w.View.Path;
        p.Curved = false;
        Click(w, screen(40, 64));
        Click(w, screen(88, 64));
        // Drag the second point.
        w.MouseDown(screen(88, 64), MouseButton.Left);
        w.MouseMove(screen(88, 80));
        w.MouseUp(screen(88, 80), MouseButton.Left);
        Assert.Equal(2, p.Points.Count);
        Assert.Equal(80, p.Points[1].Y, 0);
        // A drag on the line between them adds a point there.
        var mid = screen(64, 72);
        w.MouseDown(mid, MouseButton.Left);
        w.MouseMove(screen(64, 50));
        w.MouseUp(screen(64, 50), MouseButton.Left);
        Assert.Equal(3, p.Points.Count);
        Assert.Equal(new Vector2(64, 50), new Vector2(MathF.Round(p.Points[1].X), MathF.Round(p.Points[1].Y)));
        // Ctrl + click takes it away again.
        Click(w, screen(64, 50), RawInputModifiers.Control);
        Assert.Equal(2, p.Points.Count);
    }

    [AvaloniaFact]
    public void TheRampFollowsTheGroundUntilTypedAndAltPicksHeights()
    {
        var (w, s, screen) = Open();
        w.PathPanel.ActionBox.SelectedIndex = 1;
        Assert.Equal(PathTool.Action.Ramp, w.View.Path.Act);
        Click(w, screen(40, 64));
        Click(w, screen(88, 64));
        Assert.Equal(30, (double)w.PathPanel.StartBox.Value!, 2);
        w.PathPanel.EndBox.Value = 37;
        Assert.True(w.View.Path.RampEdited);
        Assert.Equal(37, w.View.Path.End);
        // Alt + Shift + click: the ground's height for the end.
        s.Shape(64, 20, Formula.Compile("2", []), 3, 0, "x");
        Click(w, screen(64, 20), RawInputModifiers.Alt | RawInputModifiers.Shift);
        Assert.Equal(32, w.View.Path.End, 1);
        Assert.Equal(2, w.View.Path.Points.Count);
    }
}

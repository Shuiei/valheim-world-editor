using System.Numerics;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.BareGlView;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The mouse over a 3D view in the Select tool: the look for the object under the pointer, the outline after a drag, a queued look, a click in a box, the cut, and Ctrl or Alt with the wheel.
/// </summary>
public class GlViewSelectPointerTests
{
    /// <summary>
    /// A bare view in the Select tool on a flat 2 × 2 zone area with one building piece at world 37, 30, 37 (view 5,
    /// 30, -5, its box 1 × 2 × 1).
    /// </summary>
    private static BareGlView PieceInSelect()
    {
        var b = new BareGlView(TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("wood_wall_half"), new Vector3(37, 30, 37), Vector3.Zero, 0, true)));
        b.V.SetBoxes([(0, new Vector3(4.5f, 30, -5.5f), new Vector3(5.5f, 32, -4.5f))]);
        b.V.Mode = ToolMode.Select;
        return b;
    }

    // In the Select tool the object under the pointer is looked for once for a burst of moves (a fast mouse sends many
    // a frame, and each look tries every object's box), once the moves waiting are taken; it was looked for on every
    // move.
    [AvaloniaFact]
    public void TheObjectUnderThePointerIsLookedForOnceForABurstOfMoves()
    {
        var b = PieceInSelect();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        int before = b.V.HoverPicks;
        // Three moves in a row, as the window gets them before it runs anything else (the headless MouseMove runs the
        // jobs waiting after each).
        var mouse = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        foreach (var p in new[] { b.At(-20, 30, 20), b.At(0, 30, 0), b.At(5, 31.9f, -5) })
        {
            b.Surface.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, b.Surface, mouse, b.W, p, 0, new PointerPointProperties(), KeyModifiers.None));
        }
        Assert.Equal(before, b.V.HoverPicks);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(before + 1, b.V.HoverPicks);
        Assert.Equal(0, b.V.HoverObject);
        // Off it: none.
        b.W.MouseMove(b.At(-20, 30, 20));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Null(b.V.HoverObject);
    }

    // A move queues the look, and a press before it runs drops it (no hover while the button is down): the release
    // looks again, so the object the click ended on does not wait for the next move. (An Alt click, on the ground only:
    // nothing gets selected, so no handle comes under the pointer.)
    [AvaloniaFact]
    public void TheObjectUnderThePointerIsLookedForAgainWhenAClickEnds()
    {
        var b = PieceInSelect();
        b.W.MouseMove(b.At(-20, 30, 20));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Null(b.V.HoverObject);
        // Onto the piece and pressed before the window runs anything else; the look runs while the button is down.
        var mouse = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        Point on = b.At(5, 31.9f, -5);
        b.Surface.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, b.Surface, mouse, b.W, on, 0, new PointerPointProperties(), KeyModifiers.None));
        b.Surface.RaiseEvent(new PointerPressedEventArgs(b.Surface, mouse, b.W, on, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton | RawInputModifiers.Alt, PointerUpdateKind.LeftButtonPressed), KeyModifiers.Alt, 1));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Null(b.V.HoverObject);
        b.Surface.RaiseEvent(new PointerReleasedEventArgs(b.Surface, mouse, b.W, on, 0,
            new PointerPointProperties(RawInputModifiers.Alt, PointerUpdateKind.LeftButtonReleased), KeyModifiers.Alt, MouseButton.Left));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Empty(b.V.Selected);
        Assert.Equal(0, b.V.HoverObject);
    }

    // A drag that ends over a move handle (here a zone drawn with Ctrl, which keeps the selection) finds the handle
    // again on release: the look after it outlined the object behind the handle, which a click there would not pick.
    [AvaloniaFact]
    public void AfterADragEndingOverAHandleNothingIsOutlined()
    {
        var b = new BareGlView(TestScenes.Flat(2,
            new WorldScene.Thing(1, StableHash.Of("wood_wall_half"), new Vector3(37, 30, 37), Vector3.Zero, 0, true),
            new WorldScene.Thing(2, StableHash.Of("Beech1"), new Vector3(20, 30, 20), Vector3.Zero, 1, false)));
        b.V.Mode = ToolMode.Select;
        b.V.Select([0]);
        var (c, scale) = b.V.GizmoAt()!.Value;
        // The tree's box (whatever its place) right under the east arrow.
        var east = c + new Vector3(0.7f * scale, 0, 0);
        b.V.SetBoxes([(0, new Vector3(4.5f, 30, -5.5f), new Vector3(5.5f, 32, -4.5f)), (1, east - new Vector3(0.3f, 2, 0.3f), east + new Vector3(0.3f, 0, 0.3f))]);
        Point from = b.At(-20, 30, 20), to = b.At(east.X, east.Y, east.Z);
        Assert.Equal(1, b.V.ObjectAt(to, ViewSize));
        b.W.MouseMove(from);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(b.V.SelectTool.OverHandle);
        b.W.MouseDown(from, MouseButton.Left, RawInputModifiers.Control);
        b.W.MouseMove(to, RawInputModifiers.Control | RawInputModifiers.LeftMouseButton);
        b.W.MouseUp(to, MouseButton.Left, RawInputModifiers.Control);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal([0], b.V.Selected);
        Assert.True(b.V.SelectTool.OverHandle);
        Assert.Null(b.V.HoverObject);
    }

    // A look queued by a move is dropped when the Select tool is left before it runs: no object is outlined in
    // another tool.
    [AvaloniaFact]
    public void AQueuedLookIsDroppedOnceTheSelectToolIsLeft()
    {
        var b = PieceInSelect();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        int before = b.V.HoverPicks;
        var mouse = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        b.Surface.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, b.Surface, mouse, b.W, b.At(5, 31.9f, -5), 0, new PointerPointProperties(), KeyModifiers.None));
        b.V.Mode = ToolMode.View;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(before, b.V.HoverPicks);
        Assert.Null(b.V.HoverObject);
    }

    [AvaloniaFact]
    public void AClickInABoxWithoutAModelPicksTheNearestBox()
    {
        // Without models (the test has none) a thing is its box: a beech's crown box at world 32, 30, 32 (view 0, 30, 0),
        // 12 m across and 16 m tall, over a wall piece 3 m east of its trunk. The crown's box is entered first.
        var s = TestScenes.Flat(2,
            new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 0, false),
            new WorldScene.Thing(2, StableHash.Of("wood_wall_half"), new Vector3(35, 30, 32), Vector3.Zero, 0, true));
        var b = new BareGlView(s);
        b.V.SetBoxes([(0, new Vector3(-6, 30, -6), new Vector3(6, 46, 6)), (1, new Vector3(2.5f, 30, -0.5f), new Vector3(3.5f, 31, 0.5f))]);
        Assert.Equal(ObjectKind.Trees, b.V.KindOf(0));
        b.Click(b.At(3, 30.9f, 0));
        Assert.Equal([0], b.V.Selected);
        b.Click(b.At(20, 30, 20));
        Assert.Empty(b.V.Selected);
    }

    [AvaloniaFact]
    public void TheCutHidesTheTopOfABoxFromAClick()
    {
        // A 10 m post at world 32, 30, 32 (view 0, 30, 0), seen from the side at 38 m up: with the Workshop's cut at
        // 35 m its top is not drawn, so a click on it finds nothing; without the cut it is picked.
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("wood_pole2"), new Vector3(32, 30, 32), Vector3.Zero, 0, true));
        var b = new BareGlView(s);
        b.V.SetBoxes([(0, new Vector3(-0.5f, 30, -0.5f), new Vector3(0.5f, 40, 0.5f))]);
        b.V.SetCamera(new Vector3(0, 38, 30), new Vector3(0, 38, 0), (float)(ViewSize.Width / ViewSize.Height));
        var top = b.At(0, 38, 0.5f);
        b.V.CutY = 35;
        b.Click(top);
        Assert.Empty(b.V.Selected);
        b.V.CutY = null;
        b.Click(top);
        Assert.Equal([0], b.V.Selected);
    }

    [AvaloniaFact]
    public void PasteAndShapeClicksGoToWhoeverListens()
    {
        var b = new BareGlView(TestScenes.Flat(2));
        var at = b.At(5, 30, 5);
        // Nobody listening yet: nothing happens.
        b.V.Mode = ToolMode.Paste;
        b.Click(at);
        b.V.Mode = ToolMode.Shape;
        b.Click(at);
        var pasted = new List<Vector2>();
        var shaped = new List<(float X, float Z)>();
        b.V.PasteClicked += pasted.Add;
        b.V.ShapeClicked += (x, z) => shaped.Add((x, z));
        b.Click(at);
        b.V.Mode = ToolMode.Paste;
        b.Click(at);
        AssertGrid(5, 5, (Assert.Single(pasted).X, pasted[0].Y));
        AssertGrid(5, 5, Assert.Single(shaped));
    }

    [AvaloniaFact]
    public void CtrlAndAltWithTheWheelGoToTheToolsThenZoomWhenNotUsed()
    {
        var b = new BareGlView(TestScenes.Flat(2));
        var at = b.At(0, 30, 0);
        float distance = b.V.Camera.Distance;
        var lifts = new List<(int Dir, bool Shift)>();
        var turns = new List<(Key Key, bool Shift)>();
        bool turnUsed = true;
        b.V.CtrlWheel = (dir, shift) =>
        {
            lifts.Add((dir, shift));
            return true;
        };
        b.V.AltWheel = (key, shift) =>
        {
            turns.Add((key, shift));
            return turnUsed;
        };
        // Pasting, Ctrl + a sideways notch (as some systems send Shift's): one lift.
        b.V.Mode = ToolMode.Paste;
        b.W.MouseWheel(at, new Avalonia.Vector(1, 0), RawInputModifiers.Control);
        Assert.Equal(new[] { (1, false) }, lifts);
        // Selecting, Alt + Shift + a notch towards you: one turn the way of the . key.
        b.V.Mode = ToolMode.Select;
        b.W.MouseWheel(at, new Avalonia.Vector(0, -1), RawInputModifiers.Alt | RawInputModifiers.Shift);
        Assert.Equal(new[] { (Key.OemPeriod, true) }, turns);
        Assert.Equal(distance, b.V.Camera.Distance);
        // Nothing turned: the wheel zooms.
        turnUsed = false;
        b.W.MouseWheel(at, new Avalonia.Vector(0, 1), RawInputModifiers.Alt);
        Assert.Equal((Key.OemComma, false), turns[^1]);
        Assert.Equal(distance * 0.88f, b.V.Camera.Distance, 2);
    }
}

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.KeysAndMouseSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The web editor's keys and mouse in the native app: [ ] brush size, H for the View tool, Space + left drag and Shift + right drag
/// sliding the view whatever the tool, and Alt + wheel turning the selection.
/// </summary>
[Collection("World files")]
public class KeysAndMouseTests
{
    [AvaloniaFact]
    public void BracketsMakeTheBrushSmallerAndBiggerWithinItsRange()
    {
        var (w, _, _) = Open();
        w.Tools.Choose(BrushTool.Raise);
        w.Tools.SizeSlider.Value = 8;
        w.KeyPress(Key.OemCloseBrackets, RawInputModifiers.None, PhysicalKey.BracketRight, "]");
        Assert.Equal(9, w.Tools.SizeSlider.Value);
        Assert.Equal(9, w.Tools.Brush.Radius);
        w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.None, PhysicalKey.BracketLeft, "[");
        w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.None, PhysicalKey.BracketLeft, "[");
        Assert.Equal(7, w.Tools.Brush.Radius);
        // The ends hold.
        w.Tools.SizeSlider.Value = 1.5;
        w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.None, PhysicalKey.BracketLeft, "[");
        Assert.Equal(1, w.Tools.Brush.Radius);
        w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.None, PhysicalKey.BracketLeft, "[");
        Assert.Equal(1, w.Tools.Brush.Radius);
        w.Tools.SizeSlider.Value = 30;
        w.KeyPress(Key.OemCloseBrackets, RawInputModifiers.None, PhysicalKey.BracketRight, "]");
        Assert.Equal(30, w.Tools.Brush.Radius);
        // With Ctrl, not the brush's.
        w.Tools.SizeSlider.Value = 10;
        w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.Control, PhysicalKey.BracketLeft, "[");
        Assert.Equal(10, w.Tools.Brush.Radius);
    }

    [AvaloniaFact]
    public void TheBracketsAlsoSizeThePlaceBrush()
    {
        var (w, _, _) = Open();
        w.Tools.ChooseMode(ToolMode.Place);
        w.Tools.SizeSlider.Value = 12;
        w.KeyPress(Key.OemCloseBrackets, RawInputModifiers.None, PhysicalKey.BracketRight, "]");
        Assert.Equal(13, w.PlaceTool.Brush.Radius);
    }

    // ---- H.

    [AvaloniaFact]
    public void HChoosesTheViewToolLikeEsc()
    {
        var (w, _, _) = Open();
        w.Tools.ChooseSelect();
        w.KeyPress(Key.H, RawInputModifiers.None, PhysicalKey.H, "h");
        Assert.Equal(ToolMode.View, w.Tools.Mode);
        w.Tools.Choose(BrushTool.Smooth);
        Assert.True(w.Tools.Key("H"));
        Assert.Null(w.Tools.Tool);
        Assert.Equal(ToolMode.View, w.Tools.Mode);
    }

    // ---- Sliding the view.

    [AvaloniaFact]
    public void SpaceAndLeftDragSlideTheViewInsteadOfUsingTheTool()
    {
        // The wall right of the view's middle, clear of the Select panel on the left (a press there is the panel's).
        var (w, s, screen) = Open(WallAt(80, 64));
        w.Tools.ChooseSelect();
        var (yaw, pitch, distance, target) = w.View.Camera;
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        w.MouseDown(screen(80, 64), MouseButton.Left);
        Assert.True(w.View.Panning);
        w.MouseMove(screen(100, 90));
        w.MouseUp(screen(100, 90), MouseButton.Left);
        Assert.False(w.View.Panning);
        Assert.NotEqual(target, w.View.Camera.Target);
        Assert.Equal(yaw, w.View.Camera.Yaw);
        // The Select tool did not take the click (the wall under it stays unselected).
        Assert.Empty(w.View.Selected);
        // Let go of Space: the left button is the tool's again.
        w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        w.MouseDown(screen(80, 64), MouseButton.Left);
        Assert.False(w.View.Panning);
        w.MouseUp(screen(80, 64), MouseButton.Left);
    }

    [AvaloniaFact]
    public void SpaceDragsInTheBrushToolDoNotPaint()
    {
        var (w, s, screen) = Open();
        w.Tools.Choose(BrushTool.Raise);
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        w.MouseDown(screen(64, 64), MouseButton.Left);
        Assert.False(s.Stroking);
        w.MouseUp(screen(64, 64), MouseButton.Left);
        w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Assert.False(s.CanUndo);
    }

    [AvaloniaFact]
    public void ShiftAndRightDragSlideTheViewInsteadOfTurningIt()
    {
        var (w, _, screen) = Open();
        var (yaw, pitch, distance, target) = w.View.Camera;
        w.MouseDown(screen(64, 64), MouseButton.Right, RawInputModifiers.Shift);
        w.MouseMove(screen(90, 64), RawInputModifiers.Shift);
        w.MouseUp(screen(90, 64), MouseButton.Right, RawInputModifiers.Shift);
        Assert.Equal(yaw, w.View.Camera.Yaw);
        Assert.Equal(pitch, w.View.Camera.Pitch);
        Assert.NotEqual(target, w.View.Camera.Target);
        // Without Shift the right button turns.
        w.MouseDown(screen(64, 64), MouseButton.Right);
        w.MouseMove(screen(90, 64));
        w.MouseUp(screen(90, 64), MouseButton.Right);
        Assert.NotEqual(yaw, w.View.Camera.Yaw);
    }

    [AvaloniaFact]
    public void ASpaceDragInTheViewToolPicksNothing()
    {
        var (w, _, screen) = Open(Wall());
        w.Tools.Choose(null);
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        // A click (no movement) with Space held slides, it does not pick.
        w.MouseDown(screen(40, 40), MouseButton.Left);
        w.MouseUp(screen(40, 40), MouseButton.Left);
        Assert.Empty(w.View.Selected);
        w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
    }

    [AvaloniaFact]
    public void SpaceNeverPressesTheFocusedButton()
    {
        var (w, _, _) = Open();
        int clicks = 0;
        w.AllButton.Click += (_, _) => clicks++;
        w.ShowRight(w.ViewPanelCard);
        w.AllButton.Focus();
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Assert.Equal(0, clicks);
    }
}

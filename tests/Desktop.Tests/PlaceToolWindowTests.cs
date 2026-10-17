using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool in the 3D editor: a click places what the preview shows in one step, dragging does not paint one at a
/// time, Shift-drag removes the chosen kinds, and a line is clicked and placed with Enter.
/// </summary>
public class PlaceToolWindowTests
{
    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area in the Place tool with Beech1 chosen, looking straight down;
    /// with the screen point of a grid point.
    /// </summary>
    private static (MainWindow W, EditSession S, Func<float, float, Avalonia.Point> Screen) Open()
    {
        var (w, s) = SharedPlaceWindow.Open();
        SharedPlaceWindow.ChooseBeech(w);
        return (w, s, (gx, gz) => SharedPlaceWindow.At(w, gx, gz));
    }

    [AvaloniaFact]
    public void AClickPlacesWhatThePreviewShowsInOneStep()
    {
        var (w, s, screen) = Open();
        Assert.Equal(ToolMode.Place, w.View.Mode);
        Assert.True(w.PlacePanel.Card.IsVisible);
        w.MouseMove(screen(64, 64));
        int shown = w.PlaceInput.Shown.Length;
        Assert.True(shown > 0);
        w.MouseDown(screen(64, 64), MouseButton.Left);
        w.MouseUp(screen(64, 64), MouseButton.Left);
        Assert.Equal(shown, s.Pending.Added);
        Assert.StartsWith("Placed", s.UndoLabel);
        s.Undo();
        Assert.Equal(0, s.Pending.Added);
    }

    [AvaloniaFact]
    public void OneAtATimeDoesNotPaintByDragging()
    {
        var (w, s, screen) = Open();
        w.PlaceInput.Tool.OneAtATime = true;
        w.PlaceInput.Tool.Density = 20;
        w.MouseMove(screen(64, 64));
        w.MouseDown(screen(64, 64), MouseButton.Left);
        w.MouseMove(screen(70, 64));
        // Long enough for a drag step to scatter several without One at a time.
        Thread.Sleep(80);
        typeof(PlaceInput).GetMethod("Paint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(w.PlaceInput, null);
        w.MouseUp(screen(70, 64), MouseButton.Left);
        Assert.Equal(1, s.Pending.Added);
    }

    [AvaloniaFact]
    public void ShiftDragRemovesTheChosenKinds()
    {
        var (w, s, screen) = Open();
        w.PlaceInput.Tool.OneAtATime = true;
        w.MouseMove(screen(64, 64));
        w.MouseDown(screen(64, 64), MouseButton.Left);
        w.MouseUp(screen(64, 64), MouseButton.Left);
        Assert.Equal(1, s.Pending.Added);
        w.PlaceInput.Tool.Brush.Radius = 5;
        w.MouseDown(screen(60, 64), MouseButton.Left, RawInputModifiers.Shift);
        w.MouseMove(screen(66, 64), RawInputModifiers.Shift);
        // The removal happens on the brush's timer: one frame by hand.
        typeof(PlaceInput).GetMethod("Paint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(w.PlaceInput, null);
        w.MouseUp(screen(66, 64), MouseButton.Left, RawInputModifiers.Shift);
        Assert.Equal(0, s.Pending.Added);
        Assert.Equal("Place: removed 1", s.UndoLabel);
    }

    [AvaloniaFact]
    public void ALineIsClickedAndPlacedWithEnter()
    {
        var (w, s, screen) = Open();
        w.PlacePanel.ModeButtons[PlaceTool.Modes.Line].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(PlaceTool.Modes.Line, w.PlaceTool.Mode);
        foreach (float x in new[] { 44f, 84f })
        {
            w.MouseDown(screen(x, 64), MouseButton.Left);
            w.MouseUp(screen(x, 64), MouseButton.Left);
        }
        Assert.Equal(2, w.PlaceTool.Points.Count);
        Assert.Equal(11, w.PlaceInput.Shown.Length);
        Assert.StartsWith("11 object(s) along the line", w.PlacePanel.Info.Text);
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        Assert.Equal(11, s.Pending.Added);
        Assert.Equal("Placed 11 along a line", s.UndoLabel);
        w.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Assert.Single(w.PlaceTool.Points);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Empty(w.PlaceTool.Points);
    }
}

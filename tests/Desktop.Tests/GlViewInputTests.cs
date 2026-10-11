using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The mouse and keys reach the 3D view's camera through the window (the OpenGL picture itself cannot be hit by the
/// pointer: a transparent surface over it takes the input).
/// </summary>
public class GlViewInputTests
{
    /// <summary>
    /// An 800 × 600 window shown, and its 3D view.
    /// </summary>
    private static (MainWindow Window, GlView View) Open()
    {
        var w = new MainWindow(load: false) { Width = 800, Height = 600 };
        w.Show();
        return (w, w.View);
    }

    [AvaloniaFact]
    public void RightDragTurnsTheCamera()
    {
        var (w, v) = Open();
        var (yaw, pitch, _, target) = v.Camera;
        w.MouseDown(new Point(400, 300), MouseButton.Right);
        w.MouseMove(new Point(500, 340));
        w.MouseUp(new Point(500, 340), MouseButton.Right);
        var (afterYaw, afterPitch, _, afterTarget) = v.Camera;
        Assert.Equal(yaw - 100 * 0.005f, afterYaw, 3);
        Assert.Equal(pitch + 40 * 0.005f, afterPitch, 3);
        Assert.Equal(target, afterTarget);
    }

    [AvaloniaFact]
    public void MiddleDragSlidesTheCamera()
    {
        var (w, v) = Open();
        var (yaw, _, _, target) = v.Camera;
        w.MouseDown(new Point(400, 300), MouseButton.Middle);
        w.MouseMove(new Point(460, 300));
        w.MouseUp(new Point(460, 300), MouseButton.Middle);
        Assert.NotEqual(target, v.Camera.Target);
        Assert.Equal(yaw, v.Camera.Yaw);
    }

    [AvaloniaFact]
    public void WheelZooms()
    {
        var (w, v) = Open();
        float d = v.Camera.Distance;
        w.MouseWheel(new Point(400, 300), new Vector(0, 2));
        Assert.Equal(d * 0.88f * 0.88f, v.Camera.Distance, 2);
        w.MouseWheel(new Point(400, 300), new Vector(0, -2));
        Assert.Equal(d, v.Camera.Distance, 2);
    }

    [AvaloniaFact]
    public void KeysAreHeldWhileDown()
    {
        var (w, v) = Open();
        w.KeyPress(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
        Assert.Contains(Key.W, v.KeysHeld);
        w.KeyRelease(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
        Assert.DoesNotContain(Key.W, v.KeysHeld);
    }

    [AvaloniaFact]
    public void TheInfoPanelDoesNotTakeTheViewsInput()
    {
        // A drag that starts on the panel (top left) leaves the camera alone.
        var (w, v) = Open();
        var (yaw, _, _, _) = v.Camera;
        w.MouseDown(new Point(30, 20), MouseButton.Right);
        w.MouseMove(new Point(130, 20));
        w.MouseUp(new Point(130, 20), MouseButton.Right);
        Assert.Equal(yaw, v.Camera.Yaw);
    }
}

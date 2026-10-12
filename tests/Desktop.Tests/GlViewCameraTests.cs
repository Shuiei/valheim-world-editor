using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D view's camera with the keys held: the arrows and W A S D slide the orbit camera, walking and flying go at
/// their speeds (Shift faster, Space and C up and down, never under the eyes' height), a dungeon's height is kept, and
/// with no area open the camera calls do nothing.
/// </summary>
public class GlViewCameraTests
{
    /// <summary>
    /// A window shown with its view on a flat area at 35 m (water at 30 m).
    /// </summary>
    private static (MainWindow Window, GlView View) Open()
    {
        var w = new MainWindow(load: false) { Width = 800, Height = 600 };
        w.Show();
        w.View.Show(TestScenes.FlatScene(), null);
        // Something has the focus, as after any click: with none the window's arrow keys only move the focus.
        Assert.True(w.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Focusable).Focus());
        return (w, w.View);
    }

    /// <summary>
    /// A key going down or up as the keyboard sends it (to what has the focus): one raised on the window itself with
    /// nothing focused is taken by the arrow keys' focus moves before the view sees it.
    /// </summary>
    private static void Hold(MainWindow w, Key key, bool down)
    {
        PhysicalKey physical = key switch
        {
            Key.Up => PhysicalKey.ArrowUp,
            Key.Down => PhysicalKey.ArrowDown,
            Key.Left => PhysicalKey.ArrowLeft,
            Key.Right => PhysicalKey.ArrowRight,
            Key.LeftShift => PhysicalKey.ShiftLeft,
            Key.RightShift => PhysicalKey.ShiftRight,
            Key.Space => PhysicalKey.Space,
            Key.W => PhysicalKey.W,
            Key.A => PhysicalKey.A,
            Key.S => PhysicalKey.S,
            Key.D => PhysicalKey.D,
            Key.C => PhysicalKey.C,
            _ => PhysicalKey.None,
        };
        if (down)
        {
            w.KeyPress(key, RawInputModifiers.None, physical, null);
        }
        else
        {
            w.KeyRelease(key, RawInputModifiers.None, physical, null);
        }
    }

    /// <summary>
    /// How far the eyes went along the ground with these keys held for a step of a whole second (a step is never
    /// longer than a tenth of one).
    /// </summary>
    private static float Moved(MainWindow w, GlView v, params Key[] keys)
    {
        var from = v.EyePosition;
        foreach (var k in keys)
        {
            Hold(w, k, true);
        }
        Assert.True(v.Step(1));
        foreach (var k in keys)
        {
            Hold(w, k, false);
        }
        var to = v.EyePosition;
        return Vector2.Distance(new Vector2(from.X, from.Z), new Vector2(to.X, to.Z));
    }

    [AvaloniaFact]
    public void TheArrowsAndTheLettersSlideTheOrbitCamera()
    {
        var (w, v) = Open();
        foreach (var k in new[] { Key.Up, Key.Down, Key.Left, Key.Right, Key.W, Key.S, Key.A, Key.D })
        {
            var before = v.Camera.Target;
            Hold(w, k, true);
            // No time, no step.
            Assert.False(v.Step(0));
            Assert.True(v.Step(0.05f));
            Hold(w, k, false);
            Assert.NotEqual(before, v.Camera.Target);
            // On the ground still.
            Assert.Equal(35, v.Camera.Target.Y, 3);
        }
        // Space and C only lift a flying camera: around a point they do nothing.
        foreach (var k in new[] { Key.Space, Key.C })
        {
            Hold(w, k, true);
            Assert.False(v.Step(0.1f));
            Hold(w, k, false);
        }
        Assert.Empty(v.KeysHeld);
        Assert.False(v.Step(0.1f));
    }

    [AvaloniaFact]
    public void WalkingAndFlyingGoAtTheirSpeedsAndNeverUnderTheEyes()
    {
        var (w, v) = Open();
        v.CycleEye();
        Assert.Equal(GlView.EyeMode.Walk, v.Eye);
        // Walking 6 m/s, running (Shift) 12.
        Assert.Equal(0.6f, Moved(w, v, Key.W), 3);
        Assert.Equal(1.2f, Moved(w, v, Key.Up, Key.LeftShift), 3);
        Assert.Equal(35 + 1.8f, v.EyePosition.Y, 3);
        v.CycleEye();
        Assert.Equal(GlView.EyeMode.Fly, v.Eye);
        // Flying 15 m/s, 45 with Shift.
        Assert.Equal(1.5f, Moved(w, v, Key.S), 3);
        Assert.Equal(4.5f, Moved(w, v, Key.D, Key.RightShift), 3);
        // Space goes up, C down, never under the eyes' height over the ground.
        Hold(w, Key.Space, true);
        Assert.True(v.Step(0.1f));
        Hold(w, Key.Space, false);
        Assert.Equal(35 + 1.8f + 1.5f, v.EyePosition.Y, 3);
        Hold(w, Key.C, true);
        Assert.True(v.Step(0.05f));
        Assert.Equal(35 + 1.8f + 0.75f, v.EyePosition.Y, 3);
        Assert.True(v.Step(1));
        Hold(w, Key.C, false);
        Assert.Equal(35 + 1.8f, v.EyePosition.Y, 3);
        v.CycleEye();
        Assert.Equal(GlView.EyeMode.Orbit, v.Eye);
    }

    [AvaloniaFact]
    public void InADungeonTheEyesKeepTheirHeight()
    {
        var (w, v) = Open();
        var (yaw, pitch, distance, world) = v.WorldCamera!.Value;
        // Over the game's indoors height (dungeons are 5000 m up): the ground far below does not count.
        v.WorldCamera = (yaw, pitch, distance, world with { Y = 5000 });
        Assert.Equal(5000, v.WorldCamera!.Value.World.Y, 3);
        v.CycleEye();
        Assert.Equal(5000, v.EyePosition.Y, 3);
        Assert.Equal(0.6f, Moved(w, v, Key.A), 3);
        Assert.Equal(5000, v.EyePosition.Y, 3);
        v.CycleEye();
        v.CycleEye();
        Assert.Equal(GlView.EyeMode.Orbit, v.Eye);
    }

    [AvaloniaFact]
    public void WithNoAreaTheCameraCallsDoNothing()
    {
        var w = new MainWindow(load: false) { Width = 800, Height = 600 };
        w.Show();
        var v = w.View;
        Assert.Null(v.WorldCamera);
        var before = v.Camera;
        v.WorldCamera = (1, 0.5f, 50, new Vector3(10, 20, 30));
        Assert.Equal(before, v.Camera);
        v.SetPreviews(null);
        // Nothing drawn headless: no brush point under the mouse.
        Assert.Null(v.Hover);
        // F still walks, from where the camera looks (no ground to stand on).
        v.CycleEye();
        Assert.Equal(GlView.EyeMode.Walk, v.Eye);
        Assert.Equal(before.Target, v.EyePosition);
        v.CycleEye();
        v.CycleEye();
        Assert.Equal(GlView.EyeMode.Orbit, v.Eye);
    }
}

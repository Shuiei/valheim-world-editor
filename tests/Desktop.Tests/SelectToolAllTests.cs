using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's keys, as Blender's: Ctrl + A all, Alt + A none, Ctrl + I invert, Ctrl + H hide (not drawn, not
/// picked, no dot), Alt + H show again, Shift + F or Numpad . frame; the camera does not move on them.
/// </summary>
[Collection("World files")]
public class SelectToolAllTests
{
    /// <summary>Two new walls and a tree (new: they carry green dots).</summary>
    private static View ThreeThings() => Open(T(-1, Wall, 40, 30, 40), T(-2, Wall, 44, 30, 40), T(-3, Beech, 50, 30, 50, false));

    /// <summary>A key pressed with these modifiers.</summary>
    private static void Press(View v, Key key, RawInputModifiers mods, PhysicalKey physical) => v.W.KeyPress(key, mods, physical, null);

    [AvaloniaFact]
    public void CtrlASelectsAllAltAClearsAndCtrlIInverts()
    {
        var v = ThreeThings();
        var said = MessageReader(v.Tool);
        Press(v, Key.A, RawInputModifiers.Control, PhysicalKey.A);
        Assert.Equal([0, 1, 2], v.Selected);
        Assert.Equal("Selected all 3 shown object(s).", said());
        Press(v, Key.A, RawInputModifiers.Alt, PhysicalKey.A);
        Assert.Empty(v.Selected);
        Assert.Equal("Selection cleared.", said());
        v.W.View.Select([1]);
        Press(v, Key.I, RawInputModifiers.Control, PhysicalKey.I);
        Assert.Equal([0, 2], v.Selected);
        // Neither moved the camera (A is a movement key alone).
        Assert.Equal(Vector3.Zero, v.W.View.HeldKeys().Direction);
        // A held, then Ctrl: the camera stops.
        Press(v, Key.A, RawInputModifiers.None, PhysicalKey.A);
        Assert.NotEqual(Vector3.Zero, v.W.View.HeldKeys().Direction);
        Press(v, Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft);
        Assert.Equal(Vector3.Zero, v.W.View.HeldKeys().Direction);
    }

    [AvaloniaFact]
    public void CtrlHHidesTheSelectionUntilAltHShowsItAgain()
    {
        var v = ThreeThings();
        var said = MessageReader(v.Tool);
        Press(v, Key.H, RawInputModifiers.Control, PhysicalKey.H);
        Assert.Equal("Select objects to hide first.", said());
        v.W.View.Select([0, 2]);
        Press(v, Key.H, RawInputModifiers.Control, PhysicalKey.H);
        Assert.Empty(v.Selected);
        Assert.Equal(2, v.W.View.HiddenCount);
        // Not picked by a click, Select all or a box, and no green dot.
        v.Click(40, 40);
        Assert.Empty(v.Selected);
        v.Tool.SelectAll();
        Assert.Equal([1], v.Selected);
        Assert.Single(v.W.View.NewMarkers(v.S.Scene));
        // Nor selected by a list of objects (a saved selection).
        v.W.View.Select([0, 1]);
        Assert.Equal([1], v.Selected);
        // Alt + H: back.
        Press(v, Key.H, RawInputModifiers.Alt, PhysicalKey.H);
        Assert.Equal("Showed 2 hidden object(s) again.", said());
        Assert.Equal(0, v.W.View.HiddenCount);
        v.Tool.SelectAll();
        Assert.Equal([0, 1, 2], v.Selected);
    }

    [AvaloniaFact]
    public void ShiftFFramesTheSelection()
    {
        var v = ThreeThings();
        var said = MessageReader(v.Tool);
        Press(v, Key.F, RawInputModifiers.Shift, PhysicalKey.F);
        Assert.Equal("Select objects to frame first.", said());
        v.W.View.Select([2]);
        Press(v, Key.Decimal, RawInputModifiers.None, PhysicalKey.NumPadDecimal);
        var (_, _, distance, world) = v.W.View.WorldCamera!.Value;
        Assert.Equal(50, world.X, 1);
        Assert.Equal(50, world.Z, 1);
        Assert.True(distance >= 10);
    }
}

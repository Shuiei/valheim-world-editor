using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The mouse wheel's turns: Ctrl or Alt goes to the tool only when the tool is the one that takes it (the lift with
/// Ctrl, the turns with Alt in Select, Place and Paste), each whole notch goes to the tool once, the part of a notch
/// is kept for the next wheel movement, and a wheel nothing takes zooms (TurnTool says so by its result).
/// </summary>
public class GlViewWheelTests
{
    [AvaloniaFact]
    public void CtrlWheelGoesToTheLiftOnlyWhenAToolTakesIt()
    {
        var v = new GlView { CtrlWheel = (_, _) => true, Mode = ToolMode.Paste };
        Assert.True(v.WheelWanted(ctrlW: true, altW: false));
        v.Mode = ToolMode.View;
        Assert.False(v.WheelWanted(ctrlW: true, altW: false));
        v.CtrlWheel = null;
        v.Mode = ToolMode.Paste;
        Assert.False(v.WheelWanted(ctrlW: true, altW: false));
    }

    [AvaloniaFact]
    public void AltWheelGoesToTheTurnToolsOnlyInSelectPlaceAndPaste()
    {
        var v = new GlView { AltWheel = (_, _) => true, Mode = ToolMode.Select };
        Assert.True(v.WheelWanted(ctrlW: false, altW: true));
        Assert.False(v.WheelWanted(ctrlW: false, altW: false));
        v.Mode = ToolMode.View;
        Assert.False(v.WheelWanted(ctrlW: false, altW: true));
    }

    [AvaloniaFact]
    public void EachWholeNotchGoesToTheToolAndThePartLeftOverWaits()
    {
        var v = new GlView();
        var turns = new List<(int Dir, bool Shift)>();
        v.CtrlWheel = (dir, shift) =>
        {
            turns.Add((dir, shift));
            return true;
        };
        // 2.5 notches: two turns now, the half left for the next wheel movement.
        Assert.True(v.TurnTool(new Vector(0, 2.5), shiftW: true, ctrlW: true));
        Assert.Equal([(1, true), (1, true)], turns);
        // The half and 0.6 make a whole one more.
        Assert.True(v.TurnTool(new Vector(0, 0.6), shiftW: false, ctrlW: true));
        Assert.Equal([(1, true), (1, true), (1, false)], turns);
    }

    [AvaloniaFact]
    public void TheTurnIsReversedWithTheWheelAndUsesTheKeysOfTheAltTurn()
    {
        var v = new GlView();
        var keys = new List<Key>();
        v.AltWheel = (key, _) =>
        {
            keys.Add(key);
            return true;
        };
        Assert.True(v.TurnTool(new Vector(0, -1), shiftW: false, ctrlW: false));
        Assert.True(v.TurnTool(new Vector(0, 1), shiftW: false, ctrlW: false));
        Assert.Equal([Key.OemPeriod, Key.OemComma], keys);
    }

    [AvaloniaFact]
    public void AWheelNothingTakesZoomsAndAPartNotchIsNotATurn()
    {
        var v = new GlView { CtrlWheel = (_, _) => false };
        // A whole notch the tool does not take: the view zooms.
        Assert.False(v.TurnTool(new Vector(0, 1), shiftW: false, ctrlW: true));
        // Less than a notch: nothing to turn, so the wheel is used up (no zoom) as it always was.
        Assert.True(v.TurnTool(new Vector(0, 0.3), shiftW: false, ctrlW: true));
    }

    [AvaloniaFact]
    public void AHorizontalWheelTurnsWhenThereIsNoVerticalMovement()
    {
        var v = new GlView();
        var dirs = new List<int>();
        v.CtrlWheel = (dir, _) =>
        {
            dirs.Add(dir);
            return true;
        };
        Assert.True(v.TurnTool(new Vector(-1, 0), shiftW: false, ctrlW: true));
        Assert.Equal([-1], dirs);
    }
}

using System.Numerics;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Building's own clicks on the 3D view: Shift + right click on an object asks to take it away, a middle click asks for
/// its kind; not when the pointer moved (the view turned or slid), not over nothing, not without Shift, and not when
/// not building.
/// </summary>
public class GlViewBuildClicksTests
{
    /// <summary>A bare view building, a piece at view 5, 30, -5 (its box 1 × 2 × 1), and what it asked.</summary>
    private static (BareGlView B, List<string> Asked) Building(bool building = true)
    {
        var b = new BareGlView(TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("wood_wall_half"), new Vector3(37, 30, 37), Vector3.Zero, 0, true)));
        b.V.SetBoxes([(0, new Vector3(4.5f, 30, -5.5f), new Vector3(5.5f, 32, -4.5f))]);
        b.V.Place = new PlaceInput(b.V, new PlaceTool { Scene = () => b.Scene, Building = building });
        b.V.Mode = ToolMode.Place;
        var asked = new List<string>();
        b.V.RemoveAsked += i => asked.Add($"remove {i}");
        b.V.KindAsked += i => asked.Add($"kind {i}");
        return (b, asked);
    }

    [AvaloniaFact]
    public void ShiftRightClickAsksToTakeThePieceAway()
    {
        var (b, asked) = Building();
        var on = b.At(5, 31.9f, -5);

        b.W.MouseDown(on, MouseButton.Right, RawInputModifiers.Shift);
        b.W.MouseUp(on, MouseButton.Right, RawInputModifiers.Shift);

        Assert.Equal(["remove 0"], asked);
    }

    [AvaloniaFact]
    public void AMiddleClickAsksForTheKind()
    {
        var (b, asked) = Building();
        var on = b.At(5, 31.9f, -5);

        b.W.MouseDown(on, MouseButton.Middle);
        b.W.MouseUp(on, MouseButton.Middle);

        Assert.Equal(["kind 0"], asked);
    }

    [AvaloniaFact]
    public void DragsPlainRightClicksAndEmptySpotsAskNothing()
    {
        var (b, asked) = Building();
        var on = b.At(5, 31.9f, -5);
        // A right click without Shift turns the view.
        b.W.MouseDown(on, MouseButton.Right);
        b.W.MouseUp(on, MouseButton.Right);
        // Moved 20 px: a slide.
        b.W.MouseDown(on, MouseButton.Middle);
        b.W.MouseMove(on + new Point(20, 0), RawInputModifiers.MiddleMouseButton);
        b.W.MouseUp(on + new Point(20, 0), MouseButton.Middle);
        // Over the ground only.
        var off = b.At(-20, 30, 20);
        b.W.MouseDown(off, MouseButton.Right, RawInputModifiers.Shift);
        b.W.MouseUp(off, MouseButton.Right, RawInputModifiers.Shift);
        Assert.Empty(asked);
    }

    [AvaloniaFact]
    public void NotBuildingAsksNothing()
    {
        var (b, asked) = Building(building: false);
        var on = b.At(5, 31.9f, -5);
        b.W.MouseDown(on, MouseButton.Middle);
        b.W.MouseUp(on, MouseButton.Middle);
        b.V.Mode = ToolMode.Select;
        b.W.MouseDown(on, MouseButton.Right, RawInputModifiers.Shift);
        b.W.MouseUp(on, MouseButton.Right, RawInputModifiers.Shift);
        Assert.Empty(asked);
    }
}

using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.BareGlView;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The mouse over a 3D view of its own (in a plain window, nobody else listening to it), looking straight down on a flat area at 30 m: the Dungeon tool's clicks, Alt + click with a brush, Stamp once, a click picking in the View tool, and the Paste and Shape clicks with or without a listener.
/// </summary>
public class GlViewPointerTests
{
    [AvaloniaFact]
    public void TheDungeonToolReadsTheRayUnderThePointer()
    {
        var b = new BareGlView(TestScenes.Flat(2));
        var at = b.At(0, 30, 0);
        // The ray meets the ground at the area's middle (world 32, 30, 32).
        var (o, d, ground) = b.V.WorldRay(at, ViewSize)!.Value;
        var hit = o + d * ground!.Value;
        Assert.Equal(32, hit.X, 0.5f);
        Assert.Equal(30, hit.Y, 0.5f);
        Assert.Equal(32, hit.Z, 0.5f);
        Assert.Null(b.V.WorldRay(at, default));
        // With no dungeon in the area, pointing and clicking pick nothing.
        b.V.Mode = ToolMode.Dungeon;
        b.W.MouseMove(at);
        b.Click(at);
        Assert.Null(b.V.Dungeon.HoverRoom);
        Assert.Null(b.V.Dungeon.Selected);
        Assert.Equal(ToolMode.Dungeon, b.V.Mode);
    }

    // The pointer taken away mid-stroke (a dialog, another window): the stroke ends there as one step, it does not go
    // on painting until the next click.
    [AvaloniaFact]
    public void ALostPointerEndsTheStroke()
    {
        var s = TestScenes.Flat(2);
        var b = new BareGlView(s);
        string? ended = null;
        b.V.StrokeEnded += m => ended = m.Shown;
        b.V.Tool = BrushTool.Raise;
        b.W.MouseDown(b.At(0, 30, 0), MouseButton.Left);
        Assert.True(b.V.BrushDown);
        Assert.True(s.Stroking);
        b.Surface.RaiseEvent(new PointerCaptureLostEventArgs(b.Surface, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));
        Assert.False(b.V.BrushDown);
        Assert.False(s.Stroking);
        Assert.NotNull(ended);
    }

    [AvaloniaFact]
    public void AltAndAClickWithABrushPickTheGroundsHeight()
    {
        var s = TestScenes.Flat(2);
        var b = new BareGlView(s);
        var picked = new List<(float Height, bool Mask)>();
        b.V.BrushAltClick += (h, mask) => picked.Add((h, mask));
        b.V.Tool = BrushTool.Raise;
        b.Click(b.At(5, 30, 5), RawInputModifiers.Alt);
        b.Click(b.At(-5, 30, 5), RawInputModifiers.Alt | RawInputModifiers.Shift);
        Assert.Equal(new[] { (30f, false), (30f, true) }, picked);
        // No stroke: the ground is as it was.
        Assert.False(b.V.BrushDown);
        Assert.Empty(s.UndoList);
    }

    [AvaloniaFact]
    public void StampOnceAClickOfRaiseOrLowerPutsTheStampIn()
    {
        var s = TestScenes.Flat(2);
        s.Brush.StampOnce = true;
        s.Brush.Shape = BrushShape.Stamp;
        var b = new BareGlView(s);
        var stamped = new List<(float X, float Z)>();
        b.V.StampClicked += (x, z) => stamped.Add((x, z));
        b.V.Tool = BrushTool.Raise;
        b.Click(b.At(5, 30, 5));
        b.V.Tool = BrushTool.Lower;
        b.Click(b.At(-10, 30, 0));
        Assert.Equal(2, stamped.Count);
        AssertGrid(5, 5, stamped[0]);
        AssertGrid(-10, 0, stamped[1]);
        Assert.False(b.V.BrushDown);
        // Another brush paints a stroke, even with Stamp once on; nobody listens for its end here.
        b.V.Tool = BrushTool.Smooth;
        var p = b.At(0, 30, 0);
        b.W.MouseDown(p, MouseButton.Left);
        Assert.True(b.V.BrushDown);
        b.W.MouseUp(p, MouseButton.Left);
        Assert.False(b.V.BrushDown);
        Assert.Equal(2, stamped.Count);
    }

    [AvaloniaFact]
    public void AClickInTheViewToolPicksTheObjectUnderIt()
    {
        // A building piece at world 37, 30, 37: view 5, 30, -5, its box 1 × 2 × 1.
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("wood_wall_half"), new Vector3(37, 30, 37), Vector3.Zero, 0, true));
        var b = new BareGlView(s);
        b.V.SetBoxes([(0, new Vector3(4.5f, 30, -5.5f), new Vector3(5.5f, 32, -4.5f))]);
        Assert.Equal(ObjectKind.Buildings, b.V.KindOf(0));
        Assert.Equal(ToolMode.View, b.V.Mode);
        var over = b.At(5, 31.9f, -5);
        b.Click(over);
        Assert.Equal([0], b.V.Selected);
        // Ctrl + click: taken out again; a click beside it: nothing.
        b.Click(over, RawInputModifiers.Control);
        Assert.Empty(b.V.Selected);
        b.Click(over);
        b.Click(b.At(-20, 30, 20));
        Assert.Empty(b.V.Selected);
    }

    [AvaloniaFact]
    public void APlainDoubleClickOnAnObjectInTheViewToolAsksForTheSelectTool()
    {
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("wood_wall_half"), new Vector3(37, 30, 37), Vector3.Zero, 0, true));
        var b = new BareGlView(s);
        b.V.SetBoxes([(0, new Vector3(4.5f, 30, -5.5f), new Vector3(5.5f, 32, -4.5f))]);
        int asked = 0;
        b.V.SelectToolAsked += () => asked++;
        var over = b.At(5, 31.9f, -5);
        var beside = b.At(-20, 30, 20);
        // One click only picks; a click beside it between the tries starts a new double click.
        b.Click(over);
        Assert.Equal(0, asked);
        b.Click(beside);
        b.Click(over);
        b.Click(over);
        Assert.Equal(1, asked);
        Assert.Equal([0], b.V.Selected);
        // With Ctrl (adding to the selection), or beside every object: not asked.
        b.Click(beside);
        b.Click(over, RawInputModifiers.Control);
        b.Click(over, RawInputModifiers.Control);
        b.Click(beside);
        b.Click(beside);
        Assert.Equal(1, asked);
    }
}

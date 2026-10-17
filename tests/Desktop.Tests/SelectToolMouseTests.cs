using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool with the mouse, looking straight down on a flat area: Shift and Ctrl clicks, a click on empty ground,
/// zones drawn on the ground (and cancelled), with the test setup shared through SelectToolRun.
/// </summary>
[Collection("World files")]
public class SelectToolMouseTests
{
    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area with these objects in the Select tool (see SelectToolRun).
    /// </summary>
    internal static View Open(params WorldScene.Thing[] things) => SelectToolRun.Open(things);

    /// <summary>
    /// A thing of this kind at world point (x, y, z) (see SelectToolRun).
    /// </summary>
    internal static WorldScene.Thing T(int id, int prefab, float x, float y, float z, bool piece = true) => SelectToolRun.T(id, prefab, x, y, z, piece);

    /// <summary>
    /// A box around a thing in view space (see SelectToolRun).
    /// </summary>
    internal static (int, Vector3, Vector3) Box(WorldScene s, int i) => SelectToolRun.Box(s, i);

    // Shift + click: the row from the last object clicked (w1, Shift + click w3: w1, w2, w3), not what
    // stands off it; Ctrl + click adds one.
    [AvaloniaFact]
    public void ShiftClickTakesTheRowAndCtrlClickAddsOne()
    {
        var v = Open(T(1, Wall, 36, 30, 40), T(2, Wall, 38, 30, 40), T(3, Wall, 40, 30, 40), T(4, Wall, 38, 30, 33), T(5, Wall, 46, 30, 46));
        // From the east end (the move arrows point east and north of the first one, not over the others).
        v.Click(40, 40);
        Assert.Equal([2], v.Selected);
        v.Tool.Range = true;
        v.Click(36, 40, shift: true);
        v.Tool.Range = false;
        Assert.Equal([0, 1, 2], v.Selected);
        // Ctrl (add, no row): one more.
        v.Click(38, 33, shift: true);
        Assert.Equal([0, 1, 2, 3], v.Selected);
        // A plain click: that one alone.
        v.Click(46, 46);
        Assert.Equal([4], v.Selected);
    }

    [AvaloniaFact]
    public void AClickPicksAndAClickOnEmptyGroundDeselects()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Beech, 50, 30, 50, false));
        v.Click(40, 40);
        Assert.Equal([0], v.Selected);
        v.Click(50, 50, shift: true);
        Assert.Equal([0, 1], v.Selected);
        // Shift + click on a selected one takes it out.
        v.Click(50, 50, shift: true);
        Assert.Equal([0], v.Selected);
        v.Click(30, 30);
        Assert.Empty(v.Selected);
    }

    [AvaloniaFact]
    public void AZoneDrawnOnTheGroundSelectsWhatIsInside()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Wall, 55, 30, 55));
        v.Drag(false, (36, 36), (46, 36), (46, 44), (36, 44), (36, 37));
        Assert.Equal([0, 1], v.Selected);
        Assert.Null(v.Tool.Lasso);
        // Shift: added to what is selected.
        v.Drag(true, (52, 52), (58, 52), (58, 58), (52, 58));
        Assert.Equal([0, 1, 2], v.Selected);
        // A zone with nothing in it, with Shift, keeps the selection; a plain click on the ground clears it.
        v.Drag(true, (20, 20), (24, 20), (24, 24), (20, 24));
        Assert.Equal(3, v.Selected.Count);
        v.Click(25, 25, shift: true);
        Assert.Equal(3, v.Selected.Count);
    }

    [AvaloniaFact]
    public void AZoneIsCopiedForTheViewOnlyWhenItGetsAPoint()
    {
        // Every pointer move copied all the zone's points; a move that adds none now keeps the view's copy.
        var v = Open(T(1, Wall, 40, 30, 40));
        v.Tool.Down(v.At(36, 30, 36), v.Size, false, false, 1);
        v.Tool.Moved(v.At(46, 30, 36), v.Size);
        var drawn = v.Tool.Lasso;
        Assert.Equal(2, drawn!.Length);
        v.Tool.Moved(v.At(46.1f, 30, 36), v.Size);
        Assert.Same(drawn, v.Tool.Lasso);
        v.Tool.Moved(v.At(46, 30, 44), v.Size);
        Assert.Equal(3, v.Tool.Lasso!.Length);
    }

    [AvaloniaFact]
    public void EscapeCancelsAZoneBeingDrawn()
    {
        var v = Open(T(1, Wall, 40, 30, 40));
        v.Tool.Down(v.At(36, 30, 36), v.Size, false, false, 1);
        v.Tool.Moved(v.At(46, 30, 36), v.Size);
        v.Tool.Moved(v.At(46, 30, 44), v.Size);
        Assert.NotNull(v.Tool.Lasso);
        Assert.True(v.Tool.Key(Avalonia.Input.Key.Escape, false));
        Assert.Null(v.Tool.Lasso);
    }

    [AvaloniaFact]
    public void ADoubleClickOnAnObjectInTheViewToolChoosesTheSelectToolWithItSelected()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40));
        v.W.Tools.ChooseMode(ToolMode.View);
        Assert.False(v.W.SelectPanel.Card.IsVisible);
        var p = v.At(40, 30, 40);
        for (int i = 0; i < 2; i++)
        {
            v.W.MouseDown(p, MouseButton.Left);
            v.W.MouseUp(p, MouseButton.Left);
        }
        Assert.Equal(ToolMode.Select, v.W.Tools.Mode);
        Assert.True(v.W.SelectPanel.Card.IsVisible);
        // The piece alone, not its building.
        Assert.Equal([0], v.Selected);
    }
}

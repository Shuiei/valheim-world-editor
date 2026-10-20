using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's dragging with the mouse: a selected object moved (a copy selected, undone back), a wall snapped to
/// the wall next to it, and a wall dragged far snapping to the wall where it ends.
/// </summary>
[Collection("World files")]
public class SelectToolDragTests
{
    [AvaloniaFact]
    public void DraggingASelectedObjectMovesIt()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        v.Click(40, 40);
        v.Drag(KeyModifiers.None, (40, 40), (42, 40), (44, 41));
        Assert.True(v.S.Scene.Things[0].Gone);
        Assert.Equal(44, v.Last.Position.X, 1);
        Assert.Equal(41, v.Last.Position.Z, 1);
        // The copy is selected, and one undo puts the tree back.
        Assert.Equal([v.S.Scene.Things.Count - 1], v.Selected);
        v.S.Undo();
        Assert.False(v.S.Scene.Things[0].Gone);
    }

    [AvaloniaFact]
    public void ADraggedWallSnapsToTheWallNextToIt()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 50, 30, 40));
        v.Click(50, 40);
        // Its west end brought 0.4 m from the other wall's east end: it closes the gap.
        v.Drag(KeyModifiers.None, (50, 40), (46, 40), (42.4f, 40.2f));
        var moved = v.Last;
        Assert.Equal(42, moved.Position.X, 2);
        Assert.Equal(40, moved.Position.Z, 2);
        Assert.Equal(30, moved.Position.Y, 2);
        // With snapping off, it stays where it was dropped.
        v.S.Undo();
        v.Tool.SnapToPieces = false;
        v.Click(50, 40);
        v.Drag(KeyModifiers.None, (50, 40), (46, 40), (42.4f, 40.2f));
        Assert.Equal(42.4f, v.Last.Position.X, 1);
    }

    [AvaloniaFact]
    public void AWallDraggedFarSnapsToTheWallWhereItEnds()
    {
        // Two walls 80 m apart, seen from high enough for both to be on screen.
        var v = Open(T(1, Wall, 0, 30, 40), T(2, Wall, 80, 30, 40));
        var c = new Vector3(40 - v.S.Scene.Cx, 30, -(40 - v.S.Scene.Cz));
        v.W.View.SetCamera(c + new Vector3(0, 120, 0.01f), c, 1);
        v.Click(0, 40);
        Assert.Equal([0], v.Selected);
        // The first step of the drag gathers the pieces within 60 m (none); its east end is then brought 0.4 m
        // from the other wall's west end, 80 m away: it closes the gap.
        v.Drag(KeyModifiers.None, (0, 40), (4, 40), (77.6f, 40.2f));
        var moved = v.Last;
        Assert.Equal(78, moved.Position.X, 2);
        Assert.Equal(40, moved.Position.Z, 2);
    }
}

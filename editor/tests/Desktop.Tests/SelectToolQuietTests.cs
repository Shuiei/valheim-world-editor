using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool of a view on its own, with nobody listening to its messages: nothing to do without an area, moves,
/// drops, deletes and the ways to select more still done, a move without an edit session dropped, and Whole building
/// leaving out pieces whose boxes stop short on any side.
/// </summary>
public class SelectToolQuietTests
{
    private static readonly int Beech = StableHash.Of("Beech1"), Wall = StableHash.Of("wood_wall_half");

    /// <summary>
    /// A thing of this kind at world point (x, y, z): a building piece, or a tree when not.
    /// </summary>
    private static WorldScene.Thing T(int id, int prefab, float x, float y, float z, bool piece = true) => new(id, prefab, new Vector3(x, y, z), Vector3.Zero, piece ? 0 : 1, piece);

    /// <summary>
    /// A view of its own showing a flat 2 × 2 zone area with these objects, and its edit session.
    /// </summary>
    private static (GlView V, EditSession S) View(params WorldScene.Thing[] things)
    {
        var v = new GlView();
        var s = TestScenes.Flat(2, things);
        v.Show(s.Scene, null);
        return (v, s);
    }

    [AvaloniaFact]
    public void WithoutAnAreaNothingIsDone()
    {
        var tool = new GlView().SelectTool;
        tool.Turn(5);
        tool.Lift(1);
        tool.Drop();
        tool.SameKind();
        tool.Invert();
        tool.WholeBuilding();
        tool.Delete();
        Assert.False(tool.Moving);
        Assert.Null(tool.Where());
        // No move to cancel: Esc is not the tool's.
        Assert.False(tool.Cancel());
        Assert.False(tool.Key(Key.Escape, false));
    }

    [AvaloniaFact]
    public void MovesDropsAndDeletesAreDoneWithNobodyTold()
    {
        var (v, s) = View(T(1, Beech, 40, 30, 40, false), T(2, Beech, 50, 36, 50, false), T(3, Wall, 60, 30, 60));
        var tool = v.SelectTool;
        v.Select([0]);
        tool.Lift(1);
        Assert.True(tool.Moving);
        tool.Commit();
        var lifted = s.Scene.Things[^1];
        Assert.Equal(31, lifted.Position.Y, 2);
        Assert.Equal([s.Scene.Things.Count - 1], v.Selected);
        // A turn, cancelled.
        tool.Turn(10);
        Assert.True(tool.Cancel());
        Assert.False(tool.Moving);
        // Dropped with no boxes drawn (nothing drawn here): onto the ground.
        v.Select([1]);
        tool.Drop();
        tool.Commit();
        Assert.Equal(30, s.Scene.Things[^1].Position.Y, 2);
        // The ways to select more.
        v.Select([]);
        tool.SameKind();
        Assert.Empty(v.Selected);
        v.Select([2]);
        tool.WholeBuilding();
        tool.SameKind();
        tool.Invert();
        v.Select([s.Scene.Things.Count - 1]);
        tool.WholeBuilding();
        // Deleted.
        int before = s.Scene.Things.Count(t => !t.Gone);
        v.Select([2]);
        tool.Delete();
        Assert.Equal(before - 1, s.Scene.Things.Count(t => !t.Gone));
        Assert.Empty(v.Selected);
    }

    [AvaloniaFact]
    public void ARunestoneAloneIsNotMovedWithNobodyTold()
    {
        var (v, _) = View(new WorldScene.Thing(4, StableHash.Of("Runestone_Meadows"), new Vector3(40, 30, 40), Vector3.Zero, 1, false));
        v.Select([0]);
        v.SelectTool.Lift(1);
        Assert.False(v.SelectTool.Moving);
    }

    [AvaloniaFact]
    public void WithoutAnEditSessionAMoveIsDropped()
    {
        var s = TestScenes.Flat(2, T(1, Beech, 40, 30, 40, false));
        s.Scene.Session = null;
        var v = new GlView();
        v.Show(s.Scene, null);
        v.Select([0]);
        v.SelectTool.Lift(2);
        Assert.True(v.SelectTool.Moving);
        v.SelectTool.Commit();
        Assert.False(v.SelectTool.Moving);
        Assert.Single(s.Scene.Things);
        Assert.Equal(30, s.Scene.Things[0].Position.Y);
        // Nothing to delete it from either.
        v.SelectTool.Delete();
        Assert.False(s.Scene.Things[0].Gone);
    }

    [AvaloniaFact]
    public void AMoveThatChangesNothingIsDropped()
    {
        var (v, s) = View(T(1, Beech, 40, 30, 40, false));
        v.Select([0]);
        v.SelectTool.Turn(0);
        v.SelectTool.Commit();
        Assert.Single(s.Scene.Things);
        Assert.False(s.CanUndo);
    }

    [AvaloniaFact]
    public void WholeBuildingLeavesOutPiecesThatStopShortOnAnySide()
    {
        // In view space: a piece, one touching it on the east, and one a little apart on each side of it.
        var boxes = new (Vector3 Min, Vector3 Max)[]
        {
            (new(1.5f, 1.5f, 1.5f), new(2.5f, 2.5f, 2.5f)),
            (new(2.6f, 1.5f, 1.5f), new(3.0f, 2.5f, 2.5f)),
            (new(3.4f, 1.5f, 1.5f), new(3.9f, 2.5f, 2.5f)),
            (new(0.1f, 1.5f, 1.5f), new(1.0f, 2.5f, 2.5f)),
            (new(1.5f, 3.0f, 1.5f), new(2.5f, 3.9f, 2.5f)),
            (new(1.5f, 0.1f, 1.5f), new(2.5f, 1.0f, 2.5f)),
            (new(1.5f, 1.5f, 3.0f), new(2.5f, 2.5f, 3.9f)),
            (new(1.5f, 1.5f, 0.1f), new(2.5f, 2.5f, 1.0f)),
        };
        var (v, _) = View([.. boxes.Select((_, i) => T(i + 1, Wall, 40 + i, 30, 40))]);
        v.SetBoxes(boxes.Select((b, i) => (i, b.Min, b.Max)));
        v.Select([0]);
        v.SelectTool.WholeBuilding();
        Assert.Equal([0, 1], v.Selected.Order());
    }
}

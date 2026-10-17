using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Deleting and moving objects: the edit session's changes (with undo), the Select tool's keys and exact place, and a
/// moved object saved into a world. Loads the test world: the world generator is the game's, with static state, so
/// tests that read a world never run at the same time (one collection).
/// </summary>
[Collection("World files")]
public class SelectToolTests
{
    private static readonly int Beech = StableHash.Of("Beech1");
    private static readonly int Wall = StableHash.Of("wood_wall_half");

    /// <summary>
    /// A flat 2 × 2 zone area (world x and z from -32 to 96) with a tree and a wall.
    /// </summary>
    private static EditSession Area() => TestScenes.Flat(2,
        new WorldScene.Thing(10, Beech, new Vector3(20, 30, 20), Vector3.Zero, 1, false),
        new WorldScene.Thing(11, Wall, new Vector3(40, 30, 40), new Vector3(0, 90, 0), 0, true));

    // Drop lands each object on the highest drawn top under it, whatever the size of what is under: a platform
    // across several cells of the drop's index, and a slab too wide to file by cell (looked at everywhere).
    [AvaloniaFact]
    public void DropLandsOnWideAndHugeObjectsBelow()
    {
        var s = TestScenes.Flat(2,
            new WorldScene.Thing(1, Beech, new Vector3(42, 40, 22), Vector3.Zero, 1, false),
            new WorldScene.Thing(2, Beech, new Vector3(2, 40, 62), Vector3.Zero, 1, false),
            new WorldScene.Thing(3, Wall, new Vector3(32, 30, 32), Vector3.Zero, 0, true),
            new WorldScene.Thing(4, Wall, new Vector3(32, 29, 32), Vector3.Zero, 0, true));
        var v = new GlView();
        v.Show(s.Scene, null);
        // View space: x - Cx, y, -(z - Cz).
        Vector3 V(float x, float y, float z) => new(x - s.Scene.Cx, y, -(z - s.Scene.Cz));
        v.SetBoxes([
            (0, V(41.5f, 40, 22.5f), V(42.5f, 41, 21.5f)),
            (1, V(1.5f, 40, 62.5f), V(2.5f, 41, 61.5f)),
            // A 40 m platform, 4 m high, around the first tree only.
            (2, V(12, 30, 52), V(52, 34, 12)),
            // A 300 m slab, 1 m high, under everything.
            (3, V(-118, 29, 182), V(182, 31, -118)),
        ]);
        string? said = null;
        v.SelectTool.Message += (t, _) => said = t.Shown;
        // Keeping their height (not put on the ground first), the trees drop from 40 m.
        v.SelectTool.OnGround = false;
        v.Select([0, 1]);
        v.SelectTool.Drop();
        Assert.Equal("Dropped 2 object(s): 2 onto other objects, 0 onto the ground.", said);
        v.SelectTool.Commit();
        var trees = s.Scene.Things.Where(t => !t.Gone && t.Prefab == Beech).ToList();
        Assert.Equal(34, trees.Single(t => MathF.Abs(t.Position.X - 42) < 0.01f).Position.Y, 2);
        Assert.Equal(31, trees.Single(t => MathF.Abs(t.Position.X - 2) < 0.01f).Position.Y, 2);
    }

    [Fact]
    public void DeletingHidesTheObjectsUntilUndone()
    {
        var s = Area();
        var changed = new List<int>();
        s.ThingsChanged += changed.AddRange;
        s.Delete([0]);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.Contains(10, s.Edits.Deleted);
        Assert.Equal((0, 1, 0, 0), s.Pending);
        Assert.Equal(new[] { 0 }, changed);
        Assert.Equal("Deleted 1", s.UndoLabel);
        s.Undo();
        Assert.False(s.Scene.Things[0].Gone);
        Assert.Empty(s.Edits.Deleted);
        s.Redo();
        Assert.True(s.Scene.Things[0].Gone);
    }

    [Fact]
    public void AMoveReplacesTheObjectWithACopyThatKeepsItsData()
    {
        var s = Area();
        var copies = s.Move([(0, new Vector3(25, 31, 20), new Vector3(0, 45, 0))]);
        Assert.Equal(new[] { 2 }, copies);
        Assert.True(s.Scene.Things[0].Gone);
        var copy = s.Scene.Things[2];
        Assert.Equal(new Vector3(25, 31, 20), copy.Position);
        Assert.True(copy.Id < 0);
        var added = Assert.Single(s.Edits.Added);
        // Written from the original's own data (chest contents, builder…), not as a fresh object.
        Assert.Equal(10, added.SourceId);
        Assert.False(added.Fresh);
        Assert.Equal((0, 1, 1, 0), s.Pending);

        // Moving the copy again still copies the original.
        var again = s.Move([(2, new Vector3(30, 31, 20), new Vector3(0, 45, 0))]);
        var second = s.Edits.FindAdded(s.Scene.Things[again[0]].Id)!;
        Assert.Equal(10, second.SourceId);
        Assert.Single(s.Edits.Added);

        s.Undo();
        s.Undo();
        Assert.False(s.Scene.Things[0].Gone);
        Assert.True(s.Scene.Things[2].Gone && s.Scene.Things[3].Gone);
        Assert.Equal((0, 0, 0, 0), s.Pending);
    }

    /// <summary>
    /// A shown main window editing the mound area, in the Select tool.
    /// </summary>
    private static (MainWindow W, EditSession S) Window()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 800 };
        w.Show();
        var s = Area();
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        return (w, s);
    }

    /// <summary>
    /// The last thing in the scene (the one just added).
    /// </summary>
    private static WorldScene.Thing Last(EditSession s) => s.Scene.Things[^1];

    [AvaloniaFact]
    public void TheSelectToolIsOnTheRailAndOnE()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
        w.Show();
        w.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
        Assert.True(w.Tools.SelectMode);
        Assert.Equal(ToolMode.Select, w.View.Mode);
        Assert.True(w.SelectPanel.Card.IsVisible);
        w.KeyPress(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, "1");
        Assert.Equal(ToolMode.Brush, w.View.Mode);
        Assert.False(w.SelectPanel.Card.IsVisible);
    }

    [AvaloniaFact]
    public void TurningAndLiftingWithKeysMovesTheSelectionOnceTheKeysStop()
    {
        var (w, s) = Window();
        w.View.Select([0]);
        w.KeyPress(Key.OemPeriod, RawInputModifiers.Shift, PhysicalKey.Period, ">");
        w.KeyPress(Key.PageUp, RawInputModifiers.None, PhysicalKey.PageUp, null);
        Assert.True(w.View.SelectTool.Moving);
        // The move is written when the keys stop (or at once on a commit).
        w.View.SelectTool.Commit();
        var moved = Last(s);
        Assert.Equal(15, moved.Rotation.Y, 3);
        // On the ground (30 m) and lifted a quarter metre.
        Assert.Equal(30.25f, moved.Position.Y, 3);
        Assert.Equal(new Vector2(20, 20), new Vector2(moved.Position.X, moved.Position.Z));
        Assert.Equal([s.Scene.Things.Count - 1], w.View.Selected);
        // Ctrl+Z puts it back.
        w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
        Assert.False(s.Scene.Things[0].Gone);
    }

    // A key move of the user's still waiting when Claude presses Apply in the Select panel is the user's step, and the
    // move Claude's typed place makes is Claude's: Take back Claude's changes leaves the user's.
    [AvaloniaFact]
    public async Task AUsersKeyMoveCommittedByClaudesApplyStaysTheUsers()
    {
        var (w, s) = Window();
        w.View.Select([0]);
        w.KeyPress(Key.PageUp, RawInputModifiers.None, PhysicalKey.PageUp, null);
        Assert.True(w.View.SelectTool.Moving);
        var t = new ClaudeTools(w);
        await t.SetControl("select.x", "22");
        await t.SetControl("select.apply", "click");
        Assert.Equal(["Moved 1", "Claude: Moved 1"], s.UndoList.Select(c => c.Label));
        Assert.Equal(22, Last(s).Position.X, 2);
        w.CloseWithoutAsking();
    }

    [AvaloniaFact]
    public void EscapeCancelsAMove()
    {
        var (w, s) = Window();
        w.View.Select([0]);
        w.KeyPress(Key.PageUp, RawInputModifiers.None, PhysicalKey.PageUp, null);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(w.View.SelectTool.Moving);
        Assert.Equal(2, s.Scene.Things.Count);
        // Still in the Select tool: Esc only cancelled the move.
        Assert.True(w.Tools.SelectMode);
    }

    [AvaloniaFact]
    public void DeleteKeyRemovesTheSelection()
    {
        var (w, s) = Window();
        w.View.Select([0, 1]);
        w.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        Assert.True(s.Scene.Things[0].Gone && s.Scene.Things[1].Gone);
        Assert.Empty(w.View.Selected);
        Assert.Equal(2, s.Pending.Deleted);
    }

    [AvaloniaFact]
    public void ExactPlaceMovesThereOrBy()
    {
        var (w, s) = Window();
        w.View.Select([1]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var p = w.SelectPanel;
        Assert.Equal(40, (double)p.XBox.Value!, 2);
        Assert.Equal(90, (double)p.TurnBox.Value!, 2);
        p.XBox.Value = 50;
        p.ZBox.Value = 44;
        p.TurnBox.Value = 0;
        p.ApplyButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        var moved = Last(s);
        Assert.Equal(new Vector3(50, 30, 44), moved.Position);
        Assert.Equal(0, moved.Rotation.Y, 3);
        // By: from where it is now.
        p.ByButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(0, (double)p.XBox.Value!);
        p.YBox.Value = 2;
        p.ApplyButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(new Vector3(50, 32, 44), Last(s).Position);
        // By, with all four boxes: each amount goes to its own axis.
        p.ByButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        p.XBox.Value = 1;
        p.YBox.Value = 2;
        p.ZBox.Value = -3;
        p.TurnBox.Value = 45;
        p.ApplyButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        moved = Last(s);
        Assert.Equal(51, moved.Position.X, 3);
        Assert.Equal(34, moved.Position.Y, 3);
        Assert.Equal(41, moved.Position.Z, 3);
        Assert.Equal(45, moved.Rotation.Y, 3);
    }

    [AvaloniaFact]
    public void SameKindAndInvert()
    {
        var (w, _) = Window();
        // Headless, nothing is drawn, so nothing counts as shown: both select nothing more.
        w.View.Select([0]);
        w.View.SelectTool.Invert();
        Assert.Empty(w.View.Selected);
    }

    [Fact]
    public void TheZoneSelectsWhatIsInsideIt()
    {
        var square = new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) };
        Assert.True(SelectTool.Inside(square, 5, 5));
        Assert.False(SelectTool.Inside(square, 15, 5));
        Assert.False(SelectTool.Inside(square, -1, 9));
    }

    [Fact]
    public void AMovedObjectIsSavedAtItsNewPlace()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var s = scene.Session!;
            int i = scene.Things.FindIndex(t => !t.Piece);
            Assert.True(i >= 0, "the test world has objects in zone 0, 0");
            var t = scene.Things[i];
            var to = t.Position + new Vector3(3, 0.5f, -2);
            s.Move([(i, to, t.Rotation)]);
            var res = s.Save();
            Assert.True(res.Saved, res.Message);
            Assert.Equal(1, res.ObjectsAdded);
            Assert.Equal(1, res.ObjectsDeleted);
            var again = WorldScene.Load(dir, 0, 0, 1);
            Assert.Contains(again.Things, o => o.Prefab == t.Prefab && Vector3.Distance(o.Position, to) < 0.01f);
            // Not at its old place (the kept history has it ready to come back, gone).
            Assert.DoesNotContain(again.Things, o => !o.Gone && o.Prefab == t.Prefab && Vector3.Distance(o.Position, t.Position) < 0.01f);
            // The scene stays as it is after saving (like Apply live): the same objects shown.
            Assert.Equal(again.Things.Count(o => !o.Gone), scene.Things.Count(o => !o.Gone));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    // A typed move smaller than the centimetre across or millimetre up a dragged move needs is still made: the place
    // was asked for exactly.
    [AvaloniaFact]
    public void ATypedMoveUnderACentimetreIsMade()
    {
        var (w, s) = Window();
        w.View.Select([0]);
        w.View.SelectTool.PlaceAt(0.006f, 0, 0, 0, by: true);
        Assert.True(s.CanUndo);
        Assert.Equal(20.006f, Last(s).Position.X, 4);
        float y = Last(s).Position.Y;
        w.View.SelectTool.PlaceAt(0, 0.0005f, 0, 0, by: true);
        Assert.InRange(Last(s).Position.Y - y, 0.00049f, 0.00051f);
        Assert.Equal(20.006f, Last(s).Position.X, 4);
        w.CloseWithoutAsking();
    }

    // Put on the ground, an object moved past the area's edge (no ground there) keeps its height, and the others are
    // not thrown off by it: neither a wall sinking 1000 m with the tree inside, nor the wall flying up with the tree out.
    [AvaloniaFact]
    public void AMovePastTheAreasEdgeKeepsTheHeight()
    {
        foreach (float by in new[] { 70f, -60f })
        {
            var (w, s) = Window();
            w.View.Select([0, 1]);
            w.View.SelectTool.PlaceAt(by, 0, 0, 0, by: true);
            var moved = s.Scene.Things.Skip(2).ToList();
            Assert.Equal(2, moved.Count);
            var tree = moved.Single(t => t.Prefab == Beech);
            var wall = moved.Single(t => t.Prefab == Wall);
            var (outside, inside) = by > 0 ? (wall, tree) : (tree, wall);
            Assert.True(outside.Position.X is > 96 or < -32);
            Assert.Equal(30, outside.Position.Y, 3);
            // Inside, on the ground: the tree at its foot, the wall with its bottom (0.5 m below its middle) on it.
            Assert.Equal(inside == tree ? 30 : 30.5f, inside.Position.Y, 3);
            w.CloseWithoutAsking();
        }
    }
}

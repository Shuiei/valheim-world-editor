using System.Numerics;
using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectAreaRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The edit session's object changes: delete and move (with undo), drop onto what is under the objects, the Select
/// tool's zone that selects what is inside it, and a moved object saved into a world. Loads the test world: the world
/// generator is the game's, with static state, so tests that read a world never run at the same time (one collection).
/// </summary>
[Collection("World files")]
public class SelectToolTests
{
    private static readonly int[] Expected2 = [0];
    private static readonly int[] Expected1 = [2];
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

    // A turn or lift too small to be a move (float residue) is not said: no ", turned 0°" or ", +0.00 m".
    [AvaloniaFact]
    public void ATurnOrLiftTooSmallToBeAMoveIsNotSaid()
    {
        var s = Area();
        var v = new GlView();
        v.Show(s.Scene, null);
        string? said = null;
        v.SelectTool.Message += (t, _) => said = t.English;
        v.SelectTool.OnGround = false;
        v.Select([0]);
        v.SelectTool.Lift(0.0004f);
        Assert.Equal("Moving 1 object(s): 0.0 m", said);
        v.SelectTool.Turn(0.0004f);
        Assert.Equal("Moving 1 object(s): 0.0 m", said);
        v.SelectTool.Turn(10);
        v.SelectTool.Lift(0.5f);
        Assert.Equal("Moving 1 object(s): 0.0 m, turned 10°, +0.50 m", said);
        Assert.True(v.SelectTool.Cancel());
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
        Assert.Equal(Expected2, changed);
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
        Assert.Equal(Expected1, copies);
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
}

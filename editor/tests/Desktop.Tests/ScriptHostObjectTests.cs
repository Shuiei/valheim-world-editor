using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.ScriptHostRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Script tool's objects: Place and Move refused for bad numbers and places, the turn and size scripts see, a new
/// wall snapped to the one standing (SnapPieces).
/// </summary>
public class ScriptHostObjectTests
{
    [Fact]
    public void ObjectsArePlacedOnTheNewGroundAndRemoved()
    {
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 0, false);
        var s = TestScenes.Flat(2, tree);
        var ch = Run(s, """
			var t = Objects.OfKind("Trees").Single();
			Objects.Remove(t);
			Ground.Raise(20, 20, 3);
			Objects.Place("rock4_forest", 20, 20, yaw: 90, scale: 0.5f);
			Print(Objects.CanPlace("Beech1"));
			""");
        Assert.Equal("True", ch.Output.ToString().Trim());
        ScriptHost.Apply(s, ch, "Script: test");
        Assert.True(s.Scene.Things[0].Gone);
        var rock = s.Scene.Things[^1];
        Assert.Equal(StableHash.Of("rock4_forest"), rock.Prefab);
        Assert.Equal(33, rock.Position.Y, 2);
        Assert.Equal(0.5f, rock.Scale);
    }

    /// <summary>A wooden wall standing at (20, 31, 20), its front north, as thing 0 of a flat 1-zone area.</summary>
    private static EditSession Wall() =>
        TestScenes.Flat(1, new WorldScene.Thing(7, StableHash.Of("woodwall"), new Vector3(20, 31, 20), Vector3.Zero, 0, true));

    // Objects as scripts see them carry their turn and size; a new one is put down tilted; one there is moved, turned
    // and sized in the same undo step as the rest.
    [Fact]
    public void ObjectsAreSeenTurnedAndAreMovedInTheSameStep()
    {
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), new Vector3(0, 45, 0), 1.5f, false);
        var s = TestScenes.Flat(2, tree);
        var ch = Run(s, """
			var t = Objects.All.Single();
			Print($"{t.Yaw:0} {t.Scale * 10:0}");
			Objects.Move(t, x: 12, yaw: 90, scale: 2);
			Objects.Place("rock4_forest", 20, 20, yaw: 10, pitch: 5, roll: -5);
			Ground.Raise(30, 30, 1);
			""");
        Assert.Equal("45 15", ch.Output.ToString().Trim());
        ScriptHost.Apply(s, ch, "Script: test");
        Assert.Single(s.UndoList);
        Assert.True(s.Scene.Things[0].Gone);
        var rock = s.Scene.Things.Single(t => !t.Gone && t.Prefab == StableHash.Of("rock4_forest"));
        Assert.Equal(new Vector3(5, 10, -5), rock.Rotation);
        var moved = s.Scene.Things.Single(t => !t.Gone && t.Prefab == StableHash.Of("Beech1"));
        Assert.Equal((new Vector3(12, 30, 10), new Vector3(0, 90, 0), 2f), (moved.Position, moved.Rotation, moved.Scale));
    }

    // Move: what is left out stays (as it was, or as an earlier Move left it); an object not in the area, or a size
    // not above 0, is refused.
    [Fact]
    public void AMoveKeepsWhatIsLeftOut()
    {
        var snap = ScriptHost.Take(Wall(), Terrain.PrefabCatalog.NameOf);
        var ch = new ScriptHost.Changes();
        ScriptHost.Move(snap, ch, 0, 25, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
        ScriptHost.Move(snap, ch, 0, float.NaN, float.NaN, 33, 90, float.NaN, float.NaN, 2);
        Assert.Equal((new Vector3(25, 33, 20), new Vector3(0, 90, 0), 2f), ch.Moves[0]);
        Assert.Throws<ArgumentException>(() => ScriptHost.Move(snap, ch, 9, 0, 0, 0, 0, 0, 0, 1));
        Assert.Throws<ArgumentException>(() => ScriptHost.Move(snap, ch, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    // Place and Move took NaN or infinite places and turns, negative sizes and points outside the area, and wrote them
    // into the world. They are refused; NaN still means "on the ground" for Place's y and "as it was" for Move.
    [Fact]
    public void NonNumbersSizesBelowZeroAndPlacesOutsideTheAreaAreRefused()
    {
        var snap = ScriptHost.Take(Wall(), Terrain.PrefabCatalog.NameOf);
        var ch = new ScriptHost.Changes();
        void Place(float x, float z, float yaw = 0, float scale = 0, float y = float.NaN, float pitch = 0, float roll = 0) =>
            ScriptHost.Place(snap, ch, "rock4_forest", x, z, yaw, scale, y, pitch, roll, false);
        void Move(float x = float.NaN, float z = float.NaN, float y = float.NaN, float yaw = float.NaN, float pitch = float.NaN, float roll = float.NaN) =>
            ScriptHost.Move(snap, ch, 0, x, z, y, yaw, pitch, roll, float.NaN);
        Assert.Throws<ArgumentException>(() => Place(float.NaN, 0));
        Assert.Throws<ArgumentException>(() => Place(0, float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Place(0, 0, y: float.NegativeInfinity));
        Assert.Throws<ArgumentException>(() => Place(0, 0, yaw: float.NaN));
        Assert.Throws<ArgumentException>(() => Place(0, 0, pitch: float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Place(0, 0, roll: float.NaN));
        Assert.Throws<ArgumentException>(() => Place(0, 0, scale: -1));
        Assert.Throws<ArgumentException>(() => Place(0, 0, scale: float.NaN));
        Assert.Throws<ArgumentException>(() => Place(32.5f, 0));
        Assert.Throws<ArgumentException>(() => Place(0, -40));
        Assert.Throws<ArgumentException>(() => Move(x: float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Move(y: float.NegativeInfinity));
        Assert.Throws<ArgumentException>(() => Move(yaw: float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Move(z: 100));
        Assert.Empty(ch.Add);
        Assert.Empty(ch.Moves);
        // The area's edges count; NaN y is the ground's, 0 the prefab's size.
        Place(-32, 32);
        Place(32, -32, scale: 2, y: 40);
        Move(x: -32, yaw: 90);
        Assert.Equal(2, ch.Add.Count);
        Assert.True(float.IsNaN(ch.Add[0].Position.Y));
        Assert.Equal(new Vector3(-32, 31, 20), ch.Moves[0].Position);
    }

    // A piece placed inside the area near its edge, snapped 0.4 m to a wall standing beyond the edge, would end outside
    // it: refused.
    [Fact]
    public void APieceSnappedOutsideTheAreaIsRefused()
    {
        var s = TestScenes.Flat(1, new WorldScene.Thing(7, StableHash.Of("woodwall"), new Vector3(34.2f, 31, 20), Vector3.Zero, 0, true));
        var snap = ScriptHost.Take(s, Terrain.PrefabCatalog.NameOf);
        var ch = new ScriptHost.Changes();
        ch.Add.Add((StableHash.Of("woodwall"), "woodwall", new Vector3(31.8f, 31, 20), Vector3.Zero, 0, true));
        var ex = Assert.Throws<ArgumentException>(() => ScriptHost.SnapPieces(snap, ch, TestContext.Current.CancellationToken));
        Assert.Contains("snaps to", ex.Message);
        Assert.Equal(31.8f, ch.Add[0].Position.X);
    }

    // SnapPieces: a wall placed with snap 0.3 m off meets the one standing; without snap it stays; a wall the script
    // took away is not snapped to; Stop ends the snapping.
    [Fact]
    public void NewPiecesSnapToThePiecesStanding()
    {
        var snap = ScriptHost.Take(Wall(), Terrain.PrefabCatalog.NameOf);
        int wall = StableHash.Of("woodwall");
        var ch = new ScriptHost.Changes();
        ch.Add.Add((wall, "woodwall", new Vector3(22.3f, 31, 20), Vector3.Zero, 0, true));
        ch.Add.Add((wall, "woodwall", new Vector3(17.7f, 31, 20), Vector3.Zero, 0, false));
        ScriptHost.SnapPieces(snap, ch, TestContext.Current.CancellationToken);
        Assert.Equal(22, ch.Add[0].Position.X, 3);
        Assert.Equal(17.7f, ch.Add[1].Position.X, 3);

        var gone = new ScriptHost.Changes();
        gone.Remove.Add(0);
        gone.Add.Add((wall, "woodwall", new Vector3(22.3f, 31, 20), Vector3.Zero, 0, true));
        ScriptHost.SnapPieces(snap, gone, TestContext.Current.CancellationToken);
        Assert.Equal(22.3f, gone.Add[0].Position.X, 3);

        var stopped = new ScriptHost.Changes();
        stopped.Add.Add((wall, "woodwall", new Vector3(22.3f, 31, 20), Vector3.Zero, 0, true));
        Assert.Throws<OperationCanceledException>(() => ScriptHost.SnapPieces(snap, stopped, new CancellationToken(canceled: true)));
        Assert.Equal(22.3f, stopped.Add[0].Position.X, 3);
    }
}

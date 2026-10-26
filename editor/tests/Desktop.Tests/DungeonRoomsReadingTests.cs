using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// How an area's dungeon objects are read: a room list from an added object, a damaged one said why, one whose data
/// cannot be found, and, without an edit session, the world's own object (none laid out) or nothing for a new one.
/// </summary>
[Collection("World files")]
public class DungeonRoomsReadingTests
{
    private const string NotLaidOut = "The game has not laid this dungeon out yet (nobody has been near it).";

    private static readonly int Cave = StableHash.Of("DG_Cave");

    [Fact]
    public void AddedDungeonsAreReadAndADamagedListSaysWhy()
    {
        var s = TestScenes.Flat(1);
        var good = new Vector3(10, 5100, 10);
        var damaged = new Vector3(-20, 5100, -20);
        var entrance = new Dungeons.Placed(StableHash.Of("cave_new_entrance02"), good, Quaternion.Identity);
        s.Commit("Dungeons", null, [],
        [
            (new NewObject(0, Cave, good, Vector3.Zero, 0, null, false, MadeObjects.Dungeon("DG_Cave", good, [entrance])), false),
            // Five rooms said, none there.
            (new NewObject(0, Cave, damaged, Vector3.Zero, 0, null, false, MadeObjects.Dungeon("DG_Cave", damaged, [5, 0, 0, 0])), false),
        ]);
        // An object of the area whose data is nowhere (not added through the history).
        var lost = new Vector3(30, 5100, 30);
        lock (s.Scene.Things)
        {
            s.Scene.Things.Add(new WorldScene.Thing(-999, Cave, lost, Vector3.Zero, 1, false));
        }
        var all = DungeonRooms.Of(s.Scene);
        Assert.Equal(3, all.Count);
        var read = all.Single(d => d.Thing.Position == good);
        Assert.Null(read.Problem);
        Assert.Equal(entrance.Hash, Assert.Single(read.Rooms).Hash);
        var bad = all.Single(d => d.Thing.Position == damaged);
        Assert.Empty(bad.Rooms);
        Assert.StartsWith("The dungeon's room list is damaged", bad.Problem?.English);
        Assert.Equal(NotLaidOut, all.Single(d => d.Thing.Position == lost).Problem?.English);
    }

    [AvaloniaFact]
    public void WithoutAnEditSessionTheWorldsObjectIsReadAndANewOneIsNot()
    {
        // A new object, not in the world: nothing to read.
        var made = TestScenes.FlatScene(new WorldScene.Thing(-1, Cave, new Vector3(0, 5000, 0), Vector3.Zero, 1, false));
        Assert.Equal(NotLaidOut, Assert.Single(DungeonRooms.Of(made)).Problem?.English);
        // The world's first object taken for a dungeon: its data has no room list.
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            scene.Session = null;
            lock (scene.Things)
            {
                scene.Things.Add(new WorldScene.Thing(0, Cave, new Vector3(0, 5000, 0), Vector3.Zero, 1, false));
            }
            Assert.Equal(NotLaidOut, Assert.Single(DungeonRooms.Of(scene)).Problem?.English);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}

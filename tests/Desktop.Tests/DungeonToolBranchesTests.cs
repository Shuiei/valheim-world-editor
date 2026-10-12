using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Dungeon tool's other ways, on a made-up flat area holding a dungeon object: nothing open, a room that cannot
/// join, one that would overlap or go past the dungeon's space, a door rolled and toggled, an undo that leaves the
/// hovered opening behind, end caps missing for an opening's kind, and the objects a room holds.
/// </summary>
public class DungeonToolBranchesTests
{
    private static readonly Vector3 At = new(10, 5100, 10);

    /// <summary>
    /// A flat area with one dungeon of these rooms at At, open in a Dungeon tool whose door rolls never put a door;
    /// Said keeps its messages, Changes counts its Changed.
    /// </summary>
    private sealed class Run
    {
        public EditSession S { get; } = TestScenes.Flat(1);
        public DungeonTool T { get; }
        public List<string> Said { get; } = [];
        public int Changes { get; private set; }

        public Run(string kind, params Dungeons.Placed[] rooms)
        {
            S.Commit("A dungeon", null, [], [(new NewObject(0, StableHash.Of(kind), At, Vector3.Zero, 0, null, false, MadeObjects.Dungeon(kind, At, rooms)), false)]);
            T = new DungeonTool { Scene = S.Scene, Random = () => 1f };
            T.Message += Said.Add;
            T.Changed += () => Changes++;
            T.Open(Assert.Single(T.All));
        }

        // Points the tool at a free opening from straight above it.
        public void PointAt(Dungeons.OpenEnd end) => T.Hover(end.Position + new Vector3(0, 30, 0), -Vector3.UnitY);
    }

    /// <summary>
    /// A room of this name at a place, unturned.
    /// </summary>
    private static Dungeons.Placed Room(string name, Vector3 at) => new(StableHash.Of(name), at, Quaternion.Identity);

    /// <summary>
    /// A Frost Cave's entrance room at At: three free openings.
    /// </summary>
    private static Dungeons.Placed Entrance() => Room("cave_new_entrance02", At);

    [Fact]
    public void WithNothingOpenThereIsNothingAndNothingChanges()
    {
        var t = new DungeonTool();
        int changes = 0;
        t.Changed += () => changes++;
        Assert.Empty(t.All);
        Assert.Null(t.Dungeon);
        Assert.Empty(t.FreeEnds);
        Assert.Empty(t.DoorJoints);
        Assert.Equal(0, t.OverlappingPairs);
        Assert.Empty(t.Palette);
        var joint = new Dungeons.Joint(0, 1, At, Quaternion.Identity, "", true);
        Assert.Empty(t.DoorsAt(joint));
        var room = Room("cave_new_corridor03", At);
        Assert.Null(t.Problem(room));
        Assert.Empty(t.Inside(room, []));
        Assert.Equal(0, t.CloseOpenEnds());
        Assert.False(t.Delete());
        // Over nothing: no change to tell.
        t.Hover(At + new Vector3(0, 30, 0), -Vector3.UnitY);
        Assert.Equal(0, changes);
        t.Choose("no_such_room");
        Assert.Equal("no_such_room", t.Chosen);
        Assert.Null(t.Preview);
        t.Turn();
        Assert.Equal(1, t.Join);
        Assert.False(t.Click());
        Assert.Null(t.Selected);
        // No scene: a door cannot be toggled, and nothing is told.
        t.ToggleDoor(new DungeonRooms.Dungeon(0, default, Dungeons.KindOf(StableHash.Of("DG_Cave"))!, [], null), joint);
        Assert.Equal(3, changes);
        // A scene, but no dungeon open.
        t.Scene = TestScenes.Flat(1).Scene;
        Assert.Empty(t.All);
        Assert.Null(t.Dungeon);
    }

    [Fact]
    public void AClickOverARoomSelectsItAndRoomsThatCannotJoinShowNothing()
    {
        var r = new Run("DG_Cave", Entrance());
        Assert.Equal("Frost Cave, 1 rooms, at 10, 10", DungeonTool.Describe(r.T.Dungeon!));
        // Worked out once while nothing changes.
        Assert.Same(r.T.FreeEnds, r.T.FreeEnds);
        Assert.Equal(3, r.T.FreeEnds.Count);
        Assert.Empty(r.T.DoorJoints);
        Assert.Equal(0, r.T.OverlappingPairs);
        // A room not in the catalogue, and one with no opening of the hovered one's kind: nothing to add.
        r.T.Choose("no_such_room");
        r.PointAt(r.T.FreeEnds[0]);
        Assert.Equal(0, r.T.HoverEnd);
        Assert.Null(r.T.Preview);
        r.T.Choose("cave_dome_middle");
        Assert.Null(r.T.Preview);
        // Far from the dungeon's zone.
        Assert.StartsWith("Goes past the dungeon's space", r.T.Problem(Room("cave_new_corridor03", At + new Vector3(200, 0, 0))));
        // Over nothing: the click does nothing.
        r.T.Hover(At + new Vector3(500, 40, 0), -Vector3.UnitY);
        Assert.Null(r.T.HoverEnd);
        Assert.Null(r.T.HoverRoom);
        Assert.Null(r.T.HoverJoint);
        Assert.False(r.T.Click());
        // Over the entrance room: selected, and the status line says how to delete it.
        r.T.Hover(At + new Vector3(0, 40, 0), -Vector3.UnitY);
        Assert.Equal(0, r.T.HoverRoom);
        Assert.True(r.T.Click());
        Assert.Equal(0, r.T.Selected);
        Assert.Equal("cave_new_entrance02: Delete removes it.", r.Said[^1]);
    }

    [Fact]
    public void ARoomAddedWhereAnotherStandsIsAddedWithAWarning()
    {
        // Where a corridor joins the entrance's first opening, and a corridor 1 m above that place.
        var entrance = Entrance();
        var end = Dungeons.Openings([entrance], freeOnly: true)[0];
        var corridor = Dungeons.RoomOf("cave_new_corridor03")!;
        var joined = Dungeons.Attach(corridor, Dungeons.Matching(corridor, end.Type).First(), end.Position, end.Rotation);
        var r = new Run("DG_Cave", entrance, joined with { Position = joined.Position + Vector3.UnitY });
        r.T.Choose("cave_new_corridor03");
        // From below: the entrance's opening is met before the raised corridor's.
        r.T.Hover(end.Position - new Vector3(0, 30, 0), Vector3.UnitY);
        Assert.Equal(0, r.T.HoverEnd);
        Assert.Equal("Overlaps cave_new_corridor03.", r.T.Problem(r.T.Preview!));
        Assert.True(r.T.Click());
        Assert.Equal(3, r.T.Dungeon!.Rooms.Count);
        Assert.Contains("Overlaps cave_new_corridor03.", r.Said[^1]);
        Assert.True(r.T.OverlappingPairs > 0);
    }

    [Fact]
    public void ARolledDoorIsToggledAndGoesWithItsRoom()
    {
        var r = new Run("DG_Cave", Entrance());
        // Every roll puts the door.
        r.T.Random = () => 0f;
        r.T.Choose("cave_new_crossroads01_ice");
        r.PointAt(r.T.FreeEnds[0]);
        Assert.True(r.T.Click());
        Assert.Contains("object(s) and a door", r.Said[^1]);
        Assert.Equal(1, r.T.Selected);
        var d = r.T.Dungeon!;
        var joint = Assert.Single(r.T.DoorJoints);
        Assert.Single(r.T.DoorsAt(joint));
        // Clicked away, then put back.
        r.T.Choose(null);
        r.T.Hover(joint.Position + new Vector3(0, 20, 0), -Vector3.UnitY);
        Assert.Equal(0, r.T.HoverJoint);
        Assert.True(r.T.Click());
        Assert.StartsWith("Removed the door.", r.Said[^1]);
        Assert.Empty(r.T.DoorsAt(joint));
        r.T.ToggleDoor(d, joint);
        Assert.Equal("Added a door (caverock_ice_pillar_wall). Click it again to take it away.", r.Said[^1]);
        Assert.Single(r.T.DoorsAt(joint));
        // The room deleted: what it holds and its door go with it.
        Assert.True(r.T.Delete());
        Assert.Contains("object(s) in it", r.Said[^1]);
        Assert.Single(r.T.Dungeon!.Rooms);
        Assert.Empty(r.T.DoorJoints);
    }

    [Fact]
    public void AJointOfAKindWithNoDoorOrAnAreaWithoutHistoryTakesNoDoor()
    {
        var r = new Run("DG_Cave", Entrance());
        int steps = r.S.UndoList.Count, changes = r.Changes;
        r.T.ToggleDoor(r.T.Dungeon!, new Dungeons.Joint(0, 1, At, Quaternion.Identity, "nosuchkind", true));
        Assert.Equal(steps, r.S.UndoList.Count);
        Assert.Equal(changes + 1, r.Changes);
        // Without an edit session the dungeon's rooms are not read, and nothing is toggled or told.
        r.S.Scene.Session = null;
        var d = Assert.Single(r.T.All);
        Assert.Empty(d.Rooms);
        Assert.NotNull(d.Problem);
        Assert.Empty(r.T.FreeEnds);
        r.T.ToggleDoor(d, new Dungeons.Joint(0, 1, At, Quaternion.Identity, "", true));
        Assert.Equal(changes + 1, r.Changes);
    }

    [Fact]
    public void AnUndoLeavesTheHoveredOpeningBehindAndTheSelectedRoomGone()
    {
        var r = new Run("DG_Cave", Entrance());
        r.T.Choose("cave_new_crossroads01");
        r.PointAt(r.T.FreeEnds[0]);
        Assert.True(r.T.Click());
        Assert.Equal(1, r.T.Selected);
        // One of the crossroads' openings, past the three the entrance alone has.
        var ends = r.T.FreeEnds;
        int far = Enumerable.Range(3, ends.Count - 3).Reverse().First(k =>
        {
            r.PointAt(ends[k]);
            return r.T.HoverEnd == k;
        });
        Assert.True(r.S.Undo());
        Assert.Equal(3, r.T.FreeEnds.Count);
        Assert.Equal(far, r.T.HoverEnd);
        Assert.Null(r.T.Preview);
        // The selected room is not there any more.
        Assert.False(r.T.Delete());
    }

    [Fact]
    public void OpeningsWithNoEndCapOfTheirKindStayOpen()
    {
        // An Infested Mine's divider: its opening's kind has no end cap.
        var alone = new Run("DG_DvergrTown", Room("dvergr_divider01", At));
        Assert.Equal(0, alone.T.CloseOpenEnds());
        Assert.Equal("1 open end(s) have no end cap of their type.", alone.Said[^1]);
        // With a room whose two openings have one.
        var mixed = new Run("DG_DvergrTown", Room("dvergr_divider01", At), Room("dvergr_room01", At + new Vector3(0, 0, 30)));
        Assert.Equal(3, mixed.T.FreeEnds.Count);
        Assert.Equal(2, mixed.T.CloseOpenEnds());
        Assert.Contains("Closed 2 open end(s) with end caps. 1 have no end cap of their type.", mixed.Said[^1]);
        Assert.Single(mixed.T.FreeEnds);
        // All closed: nothing more to do.
        var cave = new Run("DG_Cave", Entrance());
        Assert.Equal(3, cave.T.CloseOpenEnds());
        Assert.Equal(0, cave.T.CloseOpenEnds());
        Assert.Equal("No open end to close.", cave.Said[^1]);
    }

    [Fact]
    public void ARoomHoldsWhatIsInItsBoxAndNearerItsMiddleThanAnotherRooms()
    {
        var s = TestScenes.Flat(1);
        var p = new Vector3(0, 5000, 0);
        int chest = StableHash.Of("piece_chest_wood");
        var added = s.Commit("Things", null, [],
        [
            // Only in the room.
            (new NewObject(0, chest, p + new Vector3(-3, 0, 0), Vector3.Zero, 0), true),
            // In both, nearer the room's middle.
            (new NewObject(0, chest, p + new Vector3(2, 0, 0), Vector3.Zero, 0), true),
            // In both, nearer the other's.
            (new NewObject(0, chest, p + new Vector3(4, 0, 0), Vector3.Zero, 0), true),
            // Taken away.
            (new NewObject(0, chest, p + new Vector3(-2, 0, 1), Vector3.Zero, 0), true),
            // Outside.
            (new NewObject(0, chest, p + new Vector3(30, 0, 0), Vector3.Zero, 0), true),
            // A dungeon object: never held.
            (new NewObject(0, StableHash.Of("DG_Cave"), p, Vector3.Zero, 0), false),
        ]);
        s.Commit("Gone", null, [added[3]], []);
        var t = new DungeonTool { Scene = s.Scene };
        var room = Room("cave_new_corridor03", p);
        List<int> held = [added[0], added[1]];
        Assert.Equal(held, t.Inside(room, [room with { Position = p + new Vector3(6, 0, 0) }]));
    }

    [Fact]
    public void ARayPicksAFlatRoomAndTheNearestOfTwo()
    {
        // A divider has no width: it is picked as half a metre thick.
        var divider = Room("cave_divider", new Vector3(100, 5100, 100));
        Assert.Equal(1, DungeonTool.RoomAt([Entrance(), divider], new Vector3(100, 5140, 100), -Vector3.UnitY));
        // Pointing away.
        Assert.Null(DungeonTool.RoomAt([divider], new Vector3(100, 5140, 100), Vector3.UnitY));
        // The upper of two rooms, met first from above.
        var upper = Room("dvergr_room01", new Vector3(0, 5120, 0));
        var lower = upper with { Position = new Vector3(0, 5100, 0) };
        Assert.Equal(0, DungeonTool.RoomAt([upper, lower], new Vector3(0, 5200, 0), -Vector3.UnitY));
    }
}

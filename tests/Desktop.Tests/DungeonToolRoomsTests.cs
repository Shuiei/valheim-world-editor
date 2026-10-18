using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedDungeonToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Rooms added by the Dungeon tool: what the game puts in an added room, the doors that come with it by chance and a click on a joint toggling one, and a room placed at an open end without the pointer.
/// </summary>
[Collection("World files")]
public class DungeonToolRoomsTests
{
    [AvaloniaFact]
    public void AnAddedRoomBringsWhatTheGamePutsInItUnlessSwitchedOff()
    {
        using var r = new SharedDungeonToolRun();
        // Never a door (doors come by chance): only the room and its contents are counted.
        r.T.Random = () => 1f;
        int things = r.Scene.Things.Count;
        r.T.Choose("cave_new_crossroads01_ice");
        r.PointAt(r.T.FreeEnds[0]);
        var p = r.T.Preview!;
        var expected = Dungeons.Contents(p, r.T.Dungeon!.Kind, At, r.Scene.World.Seed);
        Assert.NotEmpty(expected);
        int steps = r.Scene.Session!.UndoList.Count;
        r.T.Click();
        // The dungeon object again, and the room's objects, in one step.
        Assert.Equal(things + 1 + expected.Count, r.Scene.Things.Count);
        Assert.Equal(steps + 1, r.Scene.Session.UndoList.Count);
        // The new room is selected, and the panel lets it be deleted at once.
        Assert.True(r.P.DeleteButton.IsEnabled);
        Assert.Contains($"with {expected.Count} object(s)", r.Said);
        Assert.All(expected, m => Assert.Contains(r.Scene.Things, t => !t.Gone && t.Prefab == StableHash.Of(m.Prefab) && Vector3.Distance(t.Position, m.Position) < 1e-3f));
        // Switched off: the room alone.
        r.P.Contents.IsChecked = false;
        r.T.Choose("cave_new_endcap02");
        r.PointAt(r.T.FreeEnds[0]);
        things = r.Scene.Things.Count;
        r.T.Click();
        Assert.Equal(things + 1, r.Scene.Things.Count);
    }

    [AvaloniaFact]
    public void DoorsComeWithAddedRoomsByChanceAndAClickOnAJointTogglesOne()
    {
        using var r = new SharedDungeonToolRun();
        int ice = StableHash.Of("caverock_ice_pillar_wall");
        int Doors() => r.Scene.Things.Count(t => !t.Gone && t.Prefab == ice);
        r.P.Contents.IsChecked = false;
        // A roll that always puts the door: the new room's joint gets the cave's door for plain openings.
        r.T.Random = () => 0f;
        r.T.Choose("cave_new_corridor03");
        r.PointAt(r.T.FreeEnds[0]);
        r.T.Click();
        Assert.Equal(1, Doors());
        Assert.Contains("with a door", r.Said);
        var joint = Assert.Single(r.T.DoorJoints);
        Assert.Single(r.T.DoorsAt(joint));
        // A roll that never does: the next room comes alone.
        r.T.Random = () => 0.99f;
        r.PointAt(r.T.FreeEnds.First(e => e.Room == 1));
        r.T.Click();
        Assert.Equal(1, Doors());
        Assert.Equal(2, r.T.DoorJoints.Count);
        // A click on the empty joint puts a door; again takes it away.
        r.T.Choose(null);
        var empty = r.T.DoorJoints.First(j => r.T.DoorsAt(j).Count == 0);
        r.T.Hover(empty.Position + new Vector3(0, 1.5f, 0) + new Vector3(0, 20, 0), -Vector3.UnitY);
        Assert.NotNull(r.T.HoverJoint);
        r.T.Click();
        Assert.Equal(2, Doors());
        r.T.Click();
        Assert.Equal(1, Doors());
        // Deleting the corridor takes its doors with it.
        r.T.Hover(r.Rooms[1].Position + new Vector3(0, 0.5f, 0), -Vector3.UnitY);
        r.T.Click();
        Assert.Equal(1, r.T.Selected);
        r.T.Delete();
        Assert.Equal(0, Doors());
    }

    // PlaceAt: the room joined at an open end by the join-th of its fitting openings, counting round either way, as
    // Preview places it; no such end, no such room, or no fitting opening: nothing.
    [AvaloniaFact]
    public void ARoomIsPlacedAtAnOpenEndWithoutThePointer()
    {
        using var r = new SharedDungeonToolRun();
        r.T.Choose("cave_new_crossroads01");
        r.PointAt(r.T.FreeEnds[0]);
        r.T.Turn();
        Assert.Equal(r.T.Preview, r.T.PlaceAt(0, "cave_new_crossroads01", 1));
        int fitting = Dungeons.Matching(Dungeons.RoomOf("cave_new_crossroads01")!, r.T.FreeEnds[0].Type).Count();
        Assert.Equal(r.T.PlaceAt(0, "cave_new_crossroads01", 1), r.T.PlaceAt(0, "cave_new_crossroads01", 1 + fitting));
        Assert.Equal(r.T.PlaceAt(0, "cave_new_crossroads01", fitting - 1), r.T.PlaceAt(0, "cave_new_crossroads01", -1));
        Assert.Null(r.T.PlaceAt(3, "cave_new_crossroads01", 0));
        Assert.Null(r.T.PlaceAt(0, "no_such_room", 0));
    }

    // Add, Remove, CloseOpenEnds and ToggleDoor name their step as asked.
}

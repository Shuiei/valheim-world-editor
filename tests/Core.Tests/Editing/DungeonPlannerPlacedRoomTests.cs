using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The best of a room's placements and the join that adds it (DungeonPlanner.Attach.Commit): a centred, straight or tall
/// placement scores higher, and the placed room is joined to its parent with its tall cells kept.
/// </summary>
public class DungeonPlannerPlacedRoomTests
{
    [Fact]
    public void ScorePlacementGivesACentredPlacementOnePointMore()
    {
        // Two placements that differ only in their shift: the centred one scores one point more, the same draw either way.
        var box = (10, 0, 4, 4);
        object centred = DungeonPlannerProbe.PlacementOf(box, 0, 0, false, [], (0, 0), (0, 0));
        object shifted = DungeonPlannerProbe.PlacementOf(box, 1, 0, false, [], (0, 0), (0, 0));
        var (pa, _, ea) = DungeonPlannerProbe.Entered(7);
        var (pb, _, eb) = DungeonPlannerProbe.Entered(7);
        float a = (float)pa.Call("ScorePlacement", centred, ea, (0, 1), false, false)!;
        float b = (float)pb.Call("ScorePlacement", shifted, eb, (0, 1), false, false)!;
        Assert.InRange(a - b, 0.9999f, 1.0001f);
    }

    [Fact]
    public void ScorePlacementGivesATallRoomThreePointsMore()
    {
        var box = (10, 0, 8, 8);
        object flat = DungeonPlannerProbe.PlacementOf(box, 0, 0, false, [], (0, 0), (0, 0));
        object tall = DungeonPlannerProbe.PlacementOf(box, 0, 0, true, [], (0, 0), (0, 0));
        var (pa, _, ea) = DungeonPlannerProbe.Entered(7);
        var (pb, _, eb) = DungeonPlannerProbe.Entered(7);
        float a = (float)pa.Call("ScorePlacement", flat, ea, (0, 1), false, false)!;
        float b = (float)pb.Call("ScorePlacement", tall, eb, (0, 1), false, true)!;
        Assert.InRange(b - a, 2.9999f, 3.0001f);
    }

    [Fact]
    public void AddPlacedRoomJoinsTheRoomToItsParentWithOneDoorway()
    {
        // The room is added off its parent with the doorway between them.
        var (p, l0, entrance) = DungeonPlannerProbe.Entered();
        object placement = DungeonPlannerProbe.PlacementOf((20, 0, 4, 4), 0, 0, false, [], (entrance.X0, entrance.Z0 + entrance.H - 1), (entrance.X0 + 1, entrance.Z0 + entrance.H - 1));
        int doors = p.Plan.Doors.Count;
        var room = (DungeonPlan.Space)p.Call("AddPlacedRoom", l0, entrance, DungeonKit.Room.Hall, placement, null)!;
        Assert.Same(entrance, room.Parent);
        Assert.Equal(doors + 1, p.Plan.Doors.Count);
        Assert.False(room.Tall);
    }

    [Fact]
    public void AddPlacedRoomKeepsTheCellsOfATallRoom()
    {
        var (p, l0, entrance) = DungeonPlannerProbe.Entered();
        object placement = DungeonPlannerProbe.PlacementOf((20, 0, 8, 8), 0, 0, true, [], (entrance.X0, entrance.Z0 + entrance.H - 1), (entrance.X0 + 1, entrance.Z0 + entrance.H - 1));
        var room = (DungeonPlan.Space)p.Call("AddPlacedRoom", l0, entrance, DungeonKit.Room.Hall, placement, null)!;
        Assert.True(room.Tall);
        Assert.Contains(DungeonPlan.CellOf((20, 0)), l0.TallCells);
    }
}

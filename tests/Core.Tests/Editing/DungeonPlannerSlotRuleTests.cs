using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where a doorway may open on a room's wall (DungeonPlanner.Geometry.SlotRules): on a stair foot only in its last two
/// metres of rows, on a cell block only at the ends of its aisle, and elsewhere clear of the other doorways of the room.
/// </summary>
public class DungeonPlannerSlotRuleTests
{
    /// <summary>A room four wide and six deep, entered from +z.</summary>
    private static DungeonPlan.Space FourWideSixDeep(DungeonKit.Room room) =>
        new() { X0 = 0, Z0 = 0, W = 4, H = 6, Dir = (0, 1), Room = room };

    private static bool StairFoot(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir) =>
        (bool)PrivateCall.Static(typeof(DungeonPlanner), "StairFootSlot", s, t1, t2, dir)!;

    private static bool CellBlock(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir) =>
        (bool)PrivateCall.Static(typeof(DungeonPlanner), "CellBlockSlot", s, t1, t2, dir)!;

    private static bool Clear(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir) =>
        (bool)PrivateCall.Static(typeof(DungeonPlanner), "ClearOfDoorways", s, t1, t2, dir)!;

    [Fact]
    public void StairFootOpensForwardAndNeverAtItsBack()
    {
        var s = FourWideSixDeep(DungeonKit.Room.StairBottom);
        Assert.True(StairFoot(s, (0, 0), (1, 0), (0, 1)));
        Assert.False(StairFoot(s, (0, 5), (1, 5), (0, -1)));
    }

    [Fact]
    public void StairFootOpensOnItsSidesOnlyInTheLastTwoMetres()
    {
        // The sides count from the way in: rows 4 and 5 of a six-deep foot are its last two metres.
        var s = FourWideSixDeep(DungeonKit.Room.StairBottom);
        Assert.True(StairFoot(s, (0, 4), (1, 5), (1, 0)));
        Assert.False(StairFoot(s, (0, 3), (1, 5), (1, 0)));
    }

    [Fact]
    public void CellBlockOpensOnlyAtTheEndsOfItsAisle()
    {
        // Across the four-wide block, the two middle tiles (columns 1 and 2) are the ones the aisle end leaves open.
        var s = FourWideSixDeep(DungeonKit.Room.Cells);
        Assert.True(CellBlock(s, (1, 5), (2, 5), (0, 1)));
        Assert.False(CellBlock(s, (0, 5), (1, 5), (0, 1)));
    }

    [Fact]
    public void CellBlockNeverOpensOnItsSides()
    {
        var s = FourWideSixDeep(DungeonKit.Room.Cells);
        Assert.False(CellBlock(s, (1, 5), (2, 5), (1, 0)));
    }

    [Fact]
    public void ClearOfDoorwaysRefusesASlotNextToADoorwayOnThatSide()
    {
        // A doorway on the room's left wall at (2,0) and (3,0); a slot at (1,0) and (1,1) is within two tiles of it.
        var room = FourWideSixDeep(DungeonKit.Room.Hall);
        var other = new DungeonPlan.Space();
        room.Doors.Add(new DungeonPlan.Doorway { A = room, B = other, T1 = (2, 0), T2 = (3, 0), Dir = (-1, 0) });
        Assert.False(Clear(room, (1, 0), (1, 1), (-1, 0)));
    }

    [Fact]
    public void ClearOfDoorwaysAllowsASlotOnAnotherSideOrFarAway()
    {
        var room = FourWideSixDeep(DungeonKit.Room.Hall);
        var other = new DungeonPlan.Space();
        room.Doors.Add(new DungeonPlan.Doorway { A = room, B = other, T1 = (2, 0), T2 = (3, 0), Dir = (-1, 0) });
        Assert.True(Clear(room, (1, 0), (1, 1), (1, 0)));
        Assert.True(Clear(room, (9, 9), (9, 10), (-1, 0)));
    }
}

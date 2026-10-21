using TerrainEditor.Editing;
using Xunit;
using Room = TerrainEditor.Editing.DungeonKit.Room;

namespace WorldEditor.Tests;

/// <summary>
/// Where a space may take a slot (a doorway in one of its walls): the two tiles of a slot are side by side, and the
/// back wall of the entrance takes none, as the portal stands before it.
/// </summary>
public sealed class DungeonPlannerSlotGeometryTests
{
    /// <summary>Calls the planner's slot check.</summary>
    private static bool IsSlot(DungeonPlan.Space space, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir) =>
        (bool)PrivateMember.Call(typeof(DungeonPlanner), "IsSlot", null, t1, t2, space, dir)!;

    /// <summary>Two tiles two apart are not one slot.</summary>
    [Fact]
    public void TilesTwoApartAreNotASlot() =>
        Assert.False(IsSlot(new DungeonPlan.Space { Room = Room.Hub }, (0, 0), (0, 2), (1, 0)));

    /// <summary>The entrance's back wall, the side opposite its facing, takes no slot.</summary>
    [Fact]
    public void TheEntrancesBackWallTakesNoSlot() =>
        Assert.False(IsSlot(new DungeonPlan.Space { Room = Room.Entrance, Dir = (0, 1) }, (0, 0), (0, 1), (0, -1)));
}

using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The twin's doorway and tiles (DungeonPlanner.Rooms.Twin.Mirror): a tile mirrors across the parent's middle, the twin's
/// doorway is the mirrored one on the opposite side, and a shaped twin keeps its tiles only when they still fit.
/// </summary>
public class DungeonPlannerMirrorTests
{
    /// <summary>A parent room of five by five tiles at the origin, and a room that opens off it along dir.</summary>
    private static (DungeonPlan.Space Parent, DungeonPlan.Space Room) ParentAndRoom((int X, int Z) dir) =>
        (new DungeonPlan.Space { X0 = 0, Z0 = 0, W = 5, H = 5, Room = DungeonKit.Room.Hall, Dir = (0, 1) },
         new DungeonPlan.Space { Dir = dir, Room = DungeonKit.Room.Hall });

    [Fact]
    public void MirrorAcrossFlipsTheWidthForARoomAlongX()
    {
        // A room along x mirrors across the width: column 0 of a five-wide parent goes to column 4.
        var (parent, room) = ParentAndRoom((1, 0));
        var mirrored = ((int, int))PrivateCall.Static(typeof(DungeonPlanner), "MirrorAcross", parent, room, (0, 2))!;
        Assert.Equal((4, 2), mirrored);
    }

    [Fact]
    public void MirrorAcrossFlipsTheDepthForARoomAlongZ()
    {
        // A room along z mirrors across the depth: row 0 of a five-deep parent goes to row 4.
        var (parent, room) = ParentAndRoom((0, 1));
        var mirrored = ((int, int))PrivateCall.Static(typeof(DungeonPlanner), "MirrorAcross", parent, room, (2, 0))!;
        Assert.Equal((2, 4), mirrored);
    }

    [Fact]
    public void MirroredTilesIsNullForAPlainBox()
    {
        // A room that is a plain box has no tiles of its own to mirror.
        var (parent, room) = ParentAndRoom((1, 0));
        Assert.Null(PrivateCall.Static(typeof(DungeonPlanner), "MirroredTiles", parent, room, null, (0, 0), (0, 1), (1, 0)));
    }

    [Fact]
    public void MirroredSlotFindsTheMirroredDoorwayOnTheOppositeSide()
    {
        // The parent's doorway at its right-hand side, tiles (3,1) and (3,2), mirrors to the left-hand side's (0,1) and (0,2).
        var (parent, room) = ParentAndRoom((1, 0));
        DungeonPlannerProbe.FillBox(parent, 4, 4);
        var first = new DungeonPlan.Doorway { T1 = (3, 1), T2 = (3, 2) };
        object?[] args = [parent, room, first, null, null, null];
        Assert.True((bool)PrivateCall.Static(typeof(DungeonPlanner), "MirroredSlot", args)!);
        Assert.Equal((-1, 0), ((int, int))args[3]!);
        Assert.Equal((0, 1), ((int, int))args[4]!);
        Assert.Equal((0, 2), ((int, int))args[5]!);
    }

    [Fact]
    public void MirroredSlotFailsWhenTheMirrorLiesOffTheSide()
    {
        // A doorway whose mirror is not a tile of the side on the opposite way has no twin there.
        var (parent, room) = ParentAndRoom((1, 0));
        DungeonPlannerProbe.FillBox(parent, 4, 4);
        var first = new DungeonPlan.Doorway { T1 = (9, 9), T2 = (9, 10) };
        Assert.False((bool)PrivateCall.Static(typeof(DungeonPlanner), "MirroredSlot", parent, room, first, null, null, null)!);
    }
}

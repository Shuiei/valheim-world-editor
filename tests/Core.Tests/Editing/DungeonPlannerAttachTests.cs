using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Choosing where a room goes off its parent (DungeonPlanner.Attach.Placing and .Size): the slot it opens at, its size,
/// the corridor it has, and the placement itself, straight or tall.
/// </summary>
public class DungeonPlannerAttachTests
{
    [Fact]
    public void ChooseSlotForwardTakesTheParentsWayAndTwoAdjacentTiles()
    {
        // Straight ahead is the parent's own way; the slot is two neighbouring tiles of the side that way.
        var (p, _, entrance) = DungeonPlannerProbe.Entered();
        object?[] args = [entrance, false, true, (0, 0), (0, 0), (0, 0)];
        Assert.True((bool)p.Call("ChooseSlot", args)!);
        Assert.Equal((0, 1), ((int, int))args[3]!);
        var t1 = ((int, int))args[4]!;
        var t2 = ((int, int))args[5]!;
        Assert.Equal(1, Math.Abs(t1.Item1 - t2.Item1) + Math.Abs(t1.Item2 - t2.Item2));
    }

    [Fact]
    public void RoomSizeRoundsATallRoomToWholeCellsOfAtLeastEight()
    {
        // A tall room spans whole cells of four metres, and at least two cells each way.
        var (p, _, _) = DungeonPlannerProbe.Entered();
        for (int i = 0; i < 20; i++)
        {
            var (w, d) = ((int, int))p.Call("RoomSize", DungeonKit.Room.Hall, true)!;
            Assert.True(w >= 8 && w % 4 == 0, $"width {w}");
            Assert.True(d >= 8 && d % 4 == 0, $"depth {d}");
        }
    }

    [Fact]
    public void CorridorLengthIsNoneOrWithinTheStylesRange()
    {
        // A room either sits wall to wall (no corridor) or has a corridor within the style's minimum and maximum.
        var style = DungeonKit.StyleOf("Crypt");
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1));
        var entrance = p.PlaceEntrance(l0);
        for (int i = 0; i < 40; i++)
        {
            int length = (int)p.Call("CorridorLength", entrance)!;
            Assert.True(length == 0 || (length >= style.CorridorMin && length <= style.CorridorMax), $"length {length}");
        }
    }

    [Fact]
    public void PlaceRoomAtOpenGroundFitsAFlatRoom()
    {
        // A straight, unbent, untall room of four by four at the entrance's front fits on open ground.
        var (p, l0, entrance) = DungeonPlannerProbe.Entered();
        var t1 = (entrance.X0, entrance.Z0 + entrance.H - 1);
        var t2 = (entrance.X0 + 1, entrance.Z0 + entrance.H - 1);
        object? placed = p.Call("PlaceRoomAt", l0, t1, t2, (0, 1), 4, 4, 0, 0, false, false, DungeonPlan.Shape.Rect);
        Assert.NotNull(placed);
        Assert.Equal(DungeonPlan.Shape.Rect, DungeonPlannerProbe.Property(placed!, "Shape"));
        Assert.Equal(false, DungeonPlannerProbe.Property(placed!, "Tall"));
    }

    [Fact]
    public void TryPlaceTallAlignedFitsATallRoomOnWholeCells()
    {
        // A tall room of eight by eight, aligned to the cells, is placed as a tall room.
        var (p, l0, entrance) = DungeonPlannerProbe.Entered();
        var t1 = (entrance.X0, entrance.Z0 + entrance.H - 1);
        var t2 = (entrance.X0 + 1, entrance.Z0 + entrance.H - 1);
        object? placed = p.Call("TryPlaceTallAligned", l0, t1, t2, (0, 1), 8, 8, 0, DungeonPlan.Shape.Rect);
        Assert.NotNull(placed);
        Assert.Equal(true, DungeonPlannerProbe.Property(placed!, "Tall"));
    }
}

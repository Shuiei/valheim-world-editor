using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The loop joins of a dungeon plan (DungeonPlanner): a loop joins two rooms by one doorway or through a corridor, the
/// join is only made when it is invoked, and a room is offered a join only to a room that is far enough away on the way.
/// </summary>
public class DungeonPlannerLoopTests
{
    /// <summary>A planner of one level with two halls side by side: the first at x 0 to 5, the second at x 6 to 8.</summary>
    private static (DungeonPlannerProbe P, DungeonPlan.Level L, DungeonPlan.Space A, DungeonPlan.Space B) TwoHalls()
    {
        var style = DungeonKit.StyleOf("Crypt");
        var p = new DungeonPlannerProbe(new DungeonGen.Settings(Size: 2, Levels: 1, Seed: 1), style, new Random(1));
        var level = p.AddLevel();
        var a = p.AddRoom(level, DungeonKit.Room.Hall, (0, 1), (0, 0, 6, 6));
        var b = p.AddRoom(level, DungeonKit.Room.Hall, (0, 1), (6, 0, 3, 6));
        return (p, level, a, b);
    }

    [Fact]
    public void MakeLoopWithoutACorridorJoinsTheRoomsByOneLoopDoorway()
    {
        // A join with no corridor (len 0) is one doorway straight from the first room to the second, marked as a loop.
        var (p, level, a, b) = TwoHalls();
        int doors = p.Plan.Doors.Count;
        p.Call("MakeLoop", level, a, b, (5, 0), (5, 1), (1, 0), 0, new List<(int, int)>());
        Assert.Equal(doors + 1, p.Plan.Doors.Count);
        var door = p.Plan.Doors[^1];
        Assert.True(door.Loop);
        Assert.Same(a, door.A);
        Assert.Same(b, door.B);
        Assert.Equal(2, level.Spaces.Count);
    }

    [Fact]
    public void MakeLoopThroughACorridorAddsTheCorridorAndTwoLoopDoorways()
    {
        // A join of len 2 runs through a corridor over the four tiles between the rooms: one corridor space, and a doorway
        // from the first room to it, then from it to the second room.
        var (p, level, a, b) = TwoHalls();
        var path = new List<(int, int)> { (6, 0), (6, 1), (7, 0), (7, 1) };
        int doors = p.Plan.Doors.Count;
        p.Call("MakeLoop", level, a, b, (5, 0), (5, 1), (1, 0), 2, path);
        Assert.Equal(doors + 2, p.Plan.Doors.Count);
        var corridor = level.Spaces[2];
        Assert.True(corridor.Corridor);
        Assert.Same(a, p.Plan.Doors[doors].A);
        Assert.Same(corridor, p.Plan.Doors[doors].B);
        Assert.Same(corridor, p.Plan.Doors[doors + 1].A);
        Assert.Same(b, p.Plan.Doors[doors + 1].B);
        Assert.All(p.Plan.Doors.Skip(doors), d => Assert.True(d.Loop));
    }

    [Fact]
    public void LoopJoinActionMakesTheJoinOnlyWhenInvoked()
    {
        // The offer to join is an action: building it changes nothing, invoking it makes the doorway.
        var (p, level, a, b) = TwoHalls();
        int doors = p.Plan.Doors.Count;
        var join = (Action)p.Call("LoopJoinAction", level, a, b, (5, 0), (5, 1), (1, 0), 0, new List<(int, int)>())!;
        Assert.Equal(doors, p.Plan.Doors.Count);
        join();
        Assert.Equal(doors + 1, p.Plan.Doors.Count);
        Assert.True(p.Plan.Doors[^1].Loop);
    }

    [Fact]
    public void AddLoopOptionsAlongOffersEachSlotOfTheSideFacingTheOtherRoom()
    {
        // The side of the first hall facing the second has five slots (six tiles), and every one of them opens onto the
        // second hall at no corridor: five offers. The other three sides face nothing, so they offer none.
        var (p, level, a, _) = TwoHalls();
        var options = new List<(float Score, Action Make)>();
        var distances = new Lazy<Dictionary<DungeonPlan.Space, int>>(() => []);
        p.Call("AddLoopOptionsAlong", level, a, (1, 0), distances, options);
        Assert.Equal(5, options.Count);
    }

    [Fact]
    public void AddLoopOptionsAlongOffersNoJoinToARoomTooCloseOnTheWay()
    {
        // A room only one room away on the way (distance below 3) is not worth a loop: the same two halls give no offer.
        var (p, level, a, b) = TwoHalls();
        var options = new List<(float Score, Action Make)>();
        var distances = new Lazy<Dictionary<DungeonPlan.Space, int>>(() => new() { [b] = 1 });
        p.Call("AddLoopOptionsAlong", level, a, (1, 0), distances, options);
        Assert.Empty(options);
    }
}

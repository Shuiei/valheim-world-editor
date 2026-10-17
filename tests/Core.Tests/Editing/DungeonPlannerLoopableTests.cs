using TerrainEditor.Editing;
using Room = TerrainEditor.Editing.DungeonKit.Room;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Which spaces of a dungeon plan may get a loop (a second door into a space already connected): an ordinary room may,
/// while a corridor, a boss room, a treasure room, a secret room and the two stair rooms may not.
/// </summary>
public sealed class DungeonPlannerLoopableTests
{
    /// <summary>Calls the planner's loop check.</summary>
    private static bool Loopable(DungeonPlan.Space space) =>
        (bool)PrivateMember.Call(typeof(DungeonPlanner), "Loopable", null, space)!;

    /// <summary>An ordinary hub room may get a loop.</summary>
    [Fact]
    public void AnOrdinaryRoomMayGetALoop() =>
        Assert.True(Loopable(new DungeonPlan.Space { Room = Room.Hub }));

    /// <summary>A corridor, which is a hub with the corridor flag, may not.</summary>
    [Fact]
    public void ACorridorMayNotGetALoop() =>
        Assert.False(Loopable(new DungeonPlan.Space { Room = Room.Hub, Corridor = true }));

    /// <summary>The rooms that must keep one door may not get a loop.</summary>
    [Theory]
    [InlineData(Room.Boss)]
    [InlineData(Room.Treasury)]
    [InlineData(Room.Secret)]
    [InlineData(Room.StairTop)]
    [InlineData(Room.StairBottom)]
    public void ARoomThatKeepsOneDoorMayNotGetALoop(Room room) =>
        Assert.False(Loopable(new DungeonPlan.Space { Room = room }));
}

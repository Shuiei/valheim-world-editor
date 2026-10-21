using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The first steps that lay a dungeon level (DungeonPlanner.Build): the entrance, the way through, the stairs down, and the
/// antechamber and arena at the way's end.
/// </summary>
public class DungeonPlannerBuildTests
{
    [Fact]
    public void PlaceEntranceCentresTheEntranceAtTheFrontOfTheFirstLevel()
    {
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 2, Seed: 1));
        var entrance = p.PlaceEntrance(l0);
        Assert.Equal(DungeonKit.Room.Entrance, entrance.Room);
        Assert.True(entrance.Main);
        Assert.Same(entrance, p.Plan.Entrance);
        Assert.Equal((0, 1), entrance.Dir);
        Assert.Equal(-entrance.W / 2, entrance.X0);
        Assert.Equal(0, entrance.Z0);
    }

    [Fact]
    public void BuildMainWayAttachesEachRoomToTheOneBeforeIt()
    {
        // Every room of the way is attached to the room before it, so walking back from the way's end reaches the entrance.
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 2, Seed: 1));
        var entrance = p.PlaceEntrance(l0);
        var end = (DungeonPlan.Space)p.Call("BuildMainWay", l0, entrance, 0, 3)!;
        Assert.True(end.Main);
        var step = end;
        for (int i = 0; i < 3 && step != entrance; i++)
        {
            step = step.Parent!;
        }
        Assert.Same(entrance, step);
    }

    [Fact]
    public void AttachLevelStairsLeadsDownToTheLevelBelow()
    {
        // The stairs' top is a stair top on this level and their foot a stair bottom on the level below.
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 2, Seed: 1));
        var l1 = p.AddLevel();
        var entrance = p.PlaceEntrance(l0);
        var top = (DungeonPlan.Space?)p.Call("AttachLevelStairs", l0, entrance);
        Assert.NotNull(top);
        Assert.Equal(DungeonKit.Room.StairTop, top!.Room);
        Assert.Contains(l1.Spaces, s => s.Room == DungeonKit.Room.StairBottom);
    }

    [Fact]
    public void PlaceArenaPutsTheBossBehindAnAntechamber()
    {
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1));
        var entrance = p.PlaceEntrance(l0);
        p.Call("PlaceArena", l0, entrance);
        Assert.NotNull(p.Plan.Boss);
        Assert.Equal(DungeonKit.Room.Boss, p.Plan.Boss!.Room);
        Assert.Equal(DungeonKit.Room.Antechamber, p.Plan.Boss.Parent?.Room);
    }

    [Fact]
    public void AttachArenaAnywhereFindsNoArenaOnAnEmptyLevel()
    {
        // With no room that can lead on, there is no place to try: null.
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1));
        object? before = p.Call("Mark", l0);
        Assert.Null(p.Call("AttachArenaAnywhere", l0, before));
    }

    [Fact]
    public void AttachArenaAnywhereOffersAnArenaOffARoomThatCanLeadOn()
    {
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1));
        p.PlaceEntrance(l0);
        object? before = p.Call("Mark", l0);
        var arena = (DungeonPlan.Space?)p.Call("AttachArenaAnywhere", l0, before);
        Assert.NotNull(arena);
        Assert.Equal(DungeonKit.Room.Boss, arena!.Room);
        Assert.Equal(DungeonKit.Room.Antechamber, arena.Parent?.Room);
    }
}

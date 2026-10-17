using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where a room's box may go in the plan: its sides on the 4-tile grid, and nothing on the level above in its cells when
/// the room is tall.
/// </summary>
public class DungeonPlannerFitTests
{
    [Fact]
    public void ABoxWhoseSidesAreAllMultiplesOfFourTilesIsWholeCells()
    {
        Assert.True(DungeonPlanner.IsWholeCells((4, 8, 12, 16)));
    }

    [Fact]
    public void ABoxWithASideOffTheFourTileGridIsNotWholeCells()
    {
        Assert.False(DungeonPlanner.IsWholeCells((4, 8, 12, 6)));
        Assert.False(DungeonPlanner.IsWholeCells((3, 8, 12, 16)));
    }

    [Fact]
    public void AnEmptyLevelAboveLeavesATallRoomsCellsClear()
    {
        var plan = new DungeonPlan();
        plan.Levels.Add(new DungeonPlan.Level { Index = 0 });
        var level = new DungeonPlan.Level { Index = 1 };
        plan.Levels.Add(level);

        Assert.True(DungeonPlanner.ClearAbove(plan, level, (0, 0, 4, 4)));
    }

    [Fact]
    public void ASpaceAboveInTheBoxsCellBlocksIt()
    {
        var plan = new DungeonPlan();
        var above = new DungeonPlan.Level { Index = 0 };
        above.Owner[(1, 1)] = new DungeonPlan.Space();
        plan.Levels.Add(above);
        var level = new DungeonPlan.Level { Index = 1 };
        plan.Levels.Add(level);

        Assert.False(DungeonPlanner.ClearAbove(plan, level, (0, 0, 4, 4)));
    }

    [Fact]
    public void ASpaceAboveOutsideTheBoxsCellsDoesNotBlockIt()
    {
        var plan = new DungeonPlan();
        var above = new DungeonPlan.Level { Index = 0 };
        above.Owner[(5, 5)] = new DungeonPlan.Space();
        plan.Levels.Add(above);
        var level = new DungeonPlan.Level { Index = 1 };
        plan.Levels.Add(level);

        Assert.True(DungeonPlanner.ClearAbove(plan, level, (0, 0, 4, 4)));
    }
}

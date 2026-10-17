using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The side rooms of a level (DungeonPlanner.Build.Wings): the rooms off the way are never on the way, and a wing reports
/// how many rooms it added.
/// </summary>
public class DungeonPlannerWingTests
{
    [Fact]
    public void PlaceWingsAddsRoomsOffTheWayAndNoneOfThemIsOnIt()
    {
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1));
        var entrance = p.PlaceEntrance(l0);
        int before = p.Plan.Spaces.Count;
        p.Call("PlaceWings", l0, 3);
        Assert.True(p.Plan.Spaces.Count > before);
        Assert.All(p.Plan.Spaces.Where(s => s != entrance), s => Assert.False(s.Main));
    }

    [Fact]
    public void AddWingReportsHowManyRoomsItAdded()
    {
        // A wing is one room, or two with its twin across the parent; placed says which.
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1));
        var entrance = p.PlaceEntrance(l0);
        object?[] args = [l0, entrance, 0];
        var wing = (DungeonPlan.Space?)p.Call("AddWing", args);
        Assert.NotNull(wing);
        Assert.False(wing!.Main);
        Assert.InRange((int)args[2]!, 1, 2);
    }
}

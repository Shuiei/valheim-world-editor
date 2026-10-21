using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// New objects a save would add: each is kept when its zone has a chunk file, and skipped with the reason otherwise.
/// </summary>
public class WorldWriterPlanNewObjectsTests
{
    /// <summary>A new object of prefab 5 at this place.</summary>
    private static NewObject At(float x, float z) => new(1, 5, new Vector3(x, 0, z), Vector3.Zero, 1f);

    [Fact]
    public void NothingAddedPlansNothing()
    {
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.PlanNewObjects(new WorldSave { Directory = "none", SaveNumber = 1 }, plan, [], (_, _) => null);

        Assert.Equal(0, plan.AddedCount);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void AnObjectOutsideTheWorldIsSkippedWithTheReason()
    {
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.PlanNewObjects(new WorldSave { Directory = "none", SaveNumber = 1 }, plan, [At(1_000_000, 0)], (_, _) => null);

        Assert.Equal(0, plan.AddedCount);
        Assert.Contains("outside the world", Assert.Single(plan.Skipped));
    }

    [Fact]
    public void AnObjectInAZoneWithoutAChunkFileIsSkippedAsNotGenerated()
    {
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.PlanNewObjects(new WorldSave { Directory = "none", SaveNumber = 1 }, plan, [At(0, 0)], (_, _) => null);

        Assert.Equal(0, plan.AddedCount);
        Assert.Contains("is not generated yet", Assert.Single(plan.Skipped));
        Assert.Empty(plan.Additions);
    }
}

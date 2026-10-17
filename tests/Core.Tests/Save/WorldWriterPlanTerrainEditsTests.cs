using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.WorldWriterTestHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// The zone edits a save would make: a zone with a terrain object gets the new ground in it, a zone without one is
/// skipped (with the reason) when it has no chunk file to go in.
/// </summary>
public class WorldWriterPlanTerrainEditsTests
{
    [Fact]
    public void AZoneWithATerrainObjectGetsTheGroundInIt()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        var edit = Raised(t.ZoneX, t.ZoneZ, 3, 500);
        var existing = new Dictionary<(int, int), List<TerrainZone>> { [(t.ZoneX, t.ZoneZ)] = [t] };
        var plan = new WorldWriter.Planned([edit], [], []);

        WorldWriter.PlanTerrainEdits(world, plan, existing, (_, _) => null);

        Assert.Equal(1, plan.Written);
        Assert.Equal(0, plan.Created);
        Assert.Contains(t.Source!.File, plan.Patches.Keys);
        Assert.Empty(plan.NotSaved);
    }

    [Fact]
    public void AnEditWithNothingChangedIsNotSaved()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var edit = new ZoneEdit(1234, -1234);
        var plan = new WorldWriter.Planned([edit], [], []);

        WorldWriter.PlanTerrainEdits(world, plan, [], (_, _) => null);

        Assert.Equal(0, plan.Written + plan.Created);
        Assert.Contains((1234, -1234), plan.NotSaved);
    }

    [Fact]
    public void AZoneWithoutAChunkFileIsSkippedWithTheReason()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var edit = Raised(0, 0, 3, 500);
        var plan = new WorldWriter.Planned([edit], [], []);

        WorldWriter.PlanTerrainEdits(world, plan, [], (_, _) => null);

        Assert.Equal(0, plan.Created);
        Assert.Contains((0, 0), plan.NotSaved);
        Assert.Contains("not generated yet", Assert.Single(plan.Skipped));
    }
}

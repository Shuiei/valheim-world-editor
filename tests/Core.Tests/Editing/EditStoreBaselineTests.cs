using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The baseline of each zone (EditStore.BaselineOf): a zone of the save has its ground as it was read, a zone with no
/// terrain data has none, and the copy handed out can be changed without changing the baseline.
/// </summary>
public class EditStoreBaselineTests
{
    [Fact]
    public void AZoneOfTheSaveHasItsGroundAsReadAsItsBaseline()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var zone = Assert.Single(world.TerrainZones);

        var baseline = edits.BaselineOf(zone.ZoneX, zone.ZoneZ);

        Assert.NotNull(baseline);
        Assert.True(baseline!.SameGround(edits.Get(zone.ZoneX, zone.ZoneZ)!));
    }

    [Fact]
    public void AZoneWithNoTerrainDataHasNoBaseline()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);

        Assert.Null(edits.BaselineOf(9999, 9999));
    }

    [Fact]
    public void ChangingTheCopyOfABaselineLeavesTheBaselineAsItWas()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var zone = Assert.Single(world.TerrainZones);
        var first = edits.BaselineOf(zone.ZoneX, zone.ZoneZ)!;
        bool changed = first.Changed;

        first.Changed = !changed;
        var again = edits.BaselineOf(zone.ZoneX, zone.ZoneZ)!;

        Assert.Equal(changed, again.Changed);
    }
}

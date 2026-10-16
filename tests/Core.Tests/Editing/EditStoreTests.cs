using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The edit store's live ground and object fields: a zone whose ground changes the game took away is dropped, a
/// field line that is not one is refused, and zone resets the game did are no longer pending.
/// </summary>
public class EditStoreTests
{
    [Fact]
    public void AZoneTheGameNoLongerChangesIsDropped()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var zone = Assert.Single(world.TerrainZones);
        Assert.NotNull(edits.Get(zone.ZoneX, zone.ZoneZ));
        // The game sends no ground changes there any more (a reset in game): taken, the zone's edit gone.
        Assert.Equal(EditStore.GameGround.Taken, edits.TakeGameGround(zone.ZoneX, zone.ZoneZ, null));
        Assert.Null(edits.Get(zone.ZoneX, zone.ZoneZ));
        // Sent again the same: nothing changes.
        Assert.Equal(EditStore.GameGround.Same, edits.TakeGameGround(zone.ZoneX, zone.ZoneZ, null));
    }

    [Fact]
    public void AFieldLineThatIsNotOneIsRefused()
    {
        Assert.Equal(new ObjectField("strings", 5, "hi"), ObjectField.Parse("strings;5;aGk="));
        Assert.Equal(new ObjectField("strings", 5, null), ObjectField.Parse("strings;5;-"));
        // Not base64, an unknown section, a key that is not a number, too few parts.
        Assert.Null(ObjectField.Parse("strings;5;not base64!"));
        Assert.Null(ObjectField.Parse("colours;5;aGk="));
        Assert.Null(ObjectField.Parse("strings;five;aGk="));
        Assert.Null(ObjectField.Parse("strings;5"));
    }

    [Fact]
    public void ZoneResetsTheGameDidAreNoLongerPending()
    {
        var edits = new EditStore(WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test"));
        var a = new ZoneReset(1, 2, true, false);
        var b = new ZoneReset(3, 4, false, true);
        edits.SetReset(a, true);
        edits.SetReset(b, true);
        var sent = edits.Resets;
        // Zone 3,4 marked again with other options while the resets were on the way: that marking is still to send.
        var again = b with { KeepBuildings = true };
        edits.SetReset(again, true);
        edits.MarkResetsApplied(sent);
        Assert.Equal(again, Assert.Single(edits.Resets));
    }

    // What changed in the zones and the object lists after a mark is undone by restoring it.
    [Fact]
    public void RestoreBringsBackTheZonesAndObjectsAsMarked()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var zone = Assert.Single(world.TerrainZones);
        var placed = new NewObject(-1, 1, System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 1f);
        var gone = placed with { Id = -2 };
        edits.AddObjects([placed, gone]);
        edits.SetDeleted([gone.Id], true);
        var lifted = edits.Get(zone.ZoneX, zone.ZoneZ)!;
        lifted.Lift[0] = 20f;
        edits.Put(lifted);
        var mark = edits.Mark();
        var flat = edits.Get(zone.ZoneX, zone.ZoneZ)!;
        Array.Clear(flat.Lift);
        edits.Put(flat);
        edits.Put(new ZoneEdit(zone.ZoneX + 1, zone.ZoneZ) { Lift = { [0] = 5f } });
        edits.SetDeleted([5, placed.Id], true);
        edits.AddObjects([placed with { Id = -3 }]);
        int version = edits.Version;
        edits.Restore(mark);
        Assert.True(edits.Version > version);
        Assert.Equal(20f, edits.Get(zone.ZoneX, zone.ZoneZ)!.Lift[0]);
        Assert.True(edits.Get(zone.ZoneX, zone.ZoneZ)!.Changed);
        Assert.Null(edits.Get(zone.ZoneX + 1, zone.ZoneZ));
        Assert.Empty(edits.Deleted);
        Assert.Equal(placed, Assert.Single(edits.Added));
        Assert.Equal(gone, edits.FindAdded(gone.Id));
        // The mark is a copy: restoring did not hand out its zones.
        edits.Put(flat);
        edits.Restore(mark);
        Assert.Equal(20f, edits.Get(zone.ZoneX, zone.ZoneZ)!.Lift[0]);
    }

    // Only the zones passing the test are copied, and they are copies.
    [Fact]
    public void CopiesTakesOnlyTheZonesPassingTheTest()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var zone = Assert.Single(world.TerrainZones);
        edits.Put(new ZoneEdit(zone.ZoneX + 1, zone.ZoneZ) { Lift = { [0] = 5f } });
        var lifted = Assert.Single(edits.Copies(e => e.HasLift));
        Assert.Equal((zone.ZoneX + 1, zone.ZoneZ), (lifted.ZoneX, lifted.ZoneZ));
        Assert.Equal(2, edits.Copies(_ => true).Count);
        Assert.Empty(edits.Copies(_ => false));
        lifted.Lift[0] = 9f;
        Assert.Equal(5f, edits.Get(zone.ZoneX + 1, zone.ZoneZ)!.Lift[0]);
    }
}

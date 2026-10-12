using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The edit store's live ground and object fields: a zone whose ground changes the game took away is dropped, and a
/// field line that is not one is refused.
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
}

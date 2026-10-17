using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A zone's ground the game changed while the editor has changes there of its own: the editor keeps its changes and asks
/// first, the game's ground waiting.
/// </summary>
public class EditStoreGameGroundTests
{
    [Fact]
    public void AZoneWithChangesKeepsThemWhenTheGameChangesItsGround()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var zone = Assert.Single(world.TerrainZones);
        var changed = new ZoneEdit(zone.ZoneX, zone.ZoneZ);
        changed.Modified[0] = true;
        changed.Level[0] = 1f;
        edits.Put(changed);

        Assert.Equal(EditStore.GameGround.Kept, edits.TakeGameGround(zone.ZoneX, zone.ZoneZ, null));
        Assert.Contains((zone.ZoneX, zone.ZoneZ), edits.GameChanged);
    }
}

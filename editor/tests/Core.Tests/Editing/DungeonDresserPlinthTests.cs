using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The plinth under a monument: floor tiles on a grid two metres apart, laid over the middle of the footprint's
/// interior, so a four by four plinth has tiles at the four points one metre either side of its centre.
/// </summary>
public class DungeonDresserPlinthTests
{
    [Fact]
    public void AFourByFourPlinthIsFourFloorTilesAtItsCentre()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = new DungeonOut();
        var dresser = (DungeonDresser)PrivateCall.Create(typeof(DungeonDresser), [typeof(DungeonKit.Material), typeof(DungeonKit.Biome), typeof(DungeonKit.Style), typeof(DungeonGen.Settings), typeof(Random), typeof(DungeonOut), typeof(string)],
            room.Material, room.Biome, room.Style, room.Settings, new Random(1), o, "Test");
        PrivateCall.Instance(dresser, "Plinth", room.Frame, 0f, 0f, 4f, 4f);
        var tiles = o.Items.Where(i => i.Prefab == "stone_floor_2x2").ToList();
        Assert.Equal(4, tiles.Count);
        Assert.Equal(4, tiles.Select(t => (t.Position.X, t.Position.Z)).Distinct().Count());
    }
}

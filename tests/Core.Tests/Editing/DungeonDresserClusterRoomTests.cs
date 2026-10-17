using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The pieces a camp and a workshop put at their anchor (DungeonDresser.ClusterCamp and ClusterWorkshop): a camp's
/// bedroll, and a workshop's bench, which the dwarven halls build of their own table.
/// </summary>
public class DungeonDresserClusterRoomTests
{
    [Fact]
    public void ACampLaysABedrollAtItsAnchor()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = DungeonDresserSupport.DressCluster(room, "camp");
        Assert.Contains(DungeonDresserSupport.At(o, room.Frame.W(0, 0, 0)), i => i.Prefab.StartsWith("Morkhalla_Bedroll", StringComparison.Ordinal));
    }

    [Fact]
    public void AWorkshopBenchIsAtItsAnchor()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = DungeonDresserSupport.DressCluster(room, "workshop");
        Assert.Contains(DungeonDresserSupport.At(o, room.Frame.W(0, 0, 0)), i => i.Prefab == "piece_workbench");
    }

    [Fact]
    public void AWorkshopInADvergrHallBuildsADvergrTableInstead()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var dvergr = new DressingRoom(room.Frame, DungeonKit.MaterialOf("Dvergr"), room.Biome, room.Style, room.Settings);
        var o = DungeonDresserSupport.DressCluster(dvergr, "workshop");
        var at = DungeonDresserSupport.At(o, room.Frame.W(0, 0, 0));
        Assert.Contains(at, i => i.Prefab == "dvergrprops_table");
        Assert.DoesNotContain(at, i => i.Prefab == "piece_workbench");
    }
}

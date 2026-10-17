using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The stock of a dungeon's clusters at the middle of a room: the armoury's stand, the bones, the store's crate and the
/// altar's idol (DungeonDresser.ClusterArmoury, ClusterBones, ClusterStores and ClusterAltar, reached through DressClusterAs).
/// </summary>
public class DungeonDresserClusterStockTests
{
    /// <summary>The middle of a room's floor, where a cluster is dressed.</summary>
    private static Vector3 Middle(DungeonDresser.Frame f) => new(f.Cx, 0, f.Cz);

    [Fact]
    public void AnArmouryHasItsWeaponStandAtTheMiddle()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = DungeonDresserSupport.DressCluster(room, "armoury");

        Assert.Contains(DungeonDresserSupport.At(o, Middle(room.Frame)), i => i.Prefab == "Morkhalla_WeaponStand");
    }

    [Fact]
    public void ABoneClusterHasABoneStackAtTheMiddle()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = DungeonDresserSupport.DressCluster(room, "bones");

        Assert.Contains(DungeonDresserSupport.At(o, Middle(room.Frame)), i => i.Prefab == "bone_stack");
    }

    [Fact]
    public void AStoreHasACrateAtTheMiddle()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = DungeonDresserSupport.DressCluster(room, "stores");

        Assert.Contains(DungeonDresserSupport.At(o, Middle(room.Frame)), i => i.Prefab is "CargoCrate" or "dvergrprops_crate");
    }

    [Fact]
    public void AnAltarInACryptHasAnIdolAtTheMiddle()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = DungeonDresserSupport.DressCluster(room, "altar");

        Assert.Contains(DungeonDresserSupport.At(o, Middle(room.Frame)),
            i => i.Prefab is "StatueEvil" or "StatueDeer" or "MountainGraveStone01");
    }
}

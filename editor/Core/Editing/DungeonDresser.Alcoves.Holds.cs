namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A keep's or prison's alcove: a sleeping bay (a bed with a chest), or a weapon or armour stand either side.</summary>
    private bool FortressAlcove(Frame f, (float U, float V, float Yaw) spot, float bu, float bv)
    {
        float yaw = spot.Yaw;
        if (Chance(0.5))
        {
            // A sleeping bay: a bed along the back, a chest at its foot.
            if (!Put(f, "bed", bu, bv, yaw + 90, 0.9f, force: true))
            {
                return false;
            }
            var (cu, cv) = Beside(spot, 1.5f);
            Put(f, "piece_chest_wood", cu, cv, yaw, 0.4f);
            return true;
        }
        foreach (int side in new[] { -1, 1 })
        {
            var (ou, ov) = Beside((bu, bv, yaw), side * 0.9f);
            Put(f, side < 0 ? "Morkhalla_WeaponStand" : One("ArmorStand_Male", "ArmorStand_Female"), ou, ov, yaw, 0.5f);
        }
        return true;
    }

    /// <summary>A dvergr hold's alcove: a shelf or a bed at the middle, with a crate, barrel or lantern beside it.</summary>
    private bool DvergrAlcove(Frame f, (float U, float V, float Yaw) spot, float bu, float bv)
    {
        float yaw = spot.Yaw;
        if (!Put(f, Chance(0.5) ? "dvergrprops_shelf" : "dvergrprops_bed", bu, bv, yaw + (Chance(0.5) ? 0 : 90), 0.8f, force: true))
        {
            return false;
        }
        var (ku, kv) = Beside(spot, 1.3f);
        Put(f, One("dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_lantern_standing"), ku, kv, R(0, 360), 0.4f);
        return true;
    }
}

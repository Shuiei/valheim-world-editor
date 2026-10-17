namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A camp: a bedroll, a fire in its pit, and a stack of wood or a barrel beside it.</summary>
    private void ClusterCamp(Frame f, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (fu, fv) = In(at, 1.5f);
        var (ru, rv) = Beside(at, 1.1f);
        Put(f, One("Morkhalla_Bedroll1", "Morkhalla_Bedroll2"), u, v, yaw + 90 + R(-10, 10), 0.8f);
        Put(f, One("Morkhalla_firepit", "fire_pit"), fu, fv, R(0, 360), 1.0f);
        Put(f, One("prop_wood_stack", "piece_pot3_cracked", "barrell"), ru, rv, R(0, 360), 0.5f);
    }

    /// <summary>A meal: a table with food and tankards, a seat each side of it.</summary>
    private void ClusterMeal(Frame f, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (table, seat, bench) = Furniture;
        if (Put(f, table, u, v, yaw + 90, 1.1f))
        {
            Put(f, Chance(0.5) ? FeastFood : "prop_Tankard", u, v, R(0, 360), 0, y: 0.82f, force: true);
            var (bu, bv) = In(at, 1.1f);
            var (ku, kv) = In(at, -1.0f);
            Put(f, bench == table ? seat : bench, bu, bv, yaw + 90, 0.4f);
            Put(f, seat, ku, kv, yaw, 0.3f);
        }
    }

    /// <summary>A scribe's desk: a table with a chair, candles or a lantern on it, and a chest beside it.</summary>
    private void ClusterScribe(Frame f, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (table, seat, _) = Furniture;
        var (ru, rv) = Beside(at, 1.1f);
        if (Put(f, table, u, v, yaw + 90, 1.0f))
        {
            // The table runs along the wall (yaw + 90): what stands on it keeps to its long axis. Only a table
            // that is a building piece carries a light the support check can see; else it stands beside.
            var (au, av) = Beside(at, 0.5f);
            var (bu2, bv2) = Beside(at, -0.6f);
            if (!Stability.Known(table))
            {
                var (su, sv) = In(at, 1.1f);
                Put(f, _mat.Name == "Dvergr" ? "dvergrprops_lantern_standing" : _b.Torch, su, sv, yaw, 0.3f);
            }
            else if (!_s.Respawn)
            {
                Put(f, "Candle_resin", au, av, R(0, 360), 0, y: 0.82f, force: true);
                Put(f, "Candle_resin", bu2, bv2, R(0, 360), 0, y: 0.82f, force: true);
            }
            else
            {
                Put(f, "piece_hoodedlantern", u, v, yaw, 0, y: 0.82f, force: true);
            }
            var (cu, cv) = In(at, 1.1f);
            Put(f, seat, cu, cv, yaw + 180, 0.3f);
            Put(f, _mat.Name == "Dvergr" ? "dvergrprops_crate" : "piece_chest_wood", ru, rv, yaw, 0.4f);
        }
    }

    /// <summary>A workshop: a bench with a tool beside it and a stack of wood or a pickaxe on the other side.</summary>
    private void ClusterWorkshop(Frame f, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (lu, lv) = Beside(at, -1.1f);
        var (ru, rv) = Beside(at, 1.1f);
        if (Put(f, _mat.Name == "Dvergr" ? "dvergrprops_table" : "piece_workbench", u, v, yaw + 90, 1.0f))
        {
            Put(f, One("prop_piece_workbench_ext1", "prop_piece_workbench_ext2"), lu, lv, yaw, 0.6f);
            Put(f, One("prop_wood_stack", "dvergrprops_pickaxe", "prop_piece_workbench_ext4"), ru, rv, yaw, 0.6f);
        }
    }
}

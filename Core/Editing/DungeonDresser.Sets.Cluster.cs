
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A cluster of things that belong together, anchored at (u, v) and facing yaw (into the room): a camp (bedroll,
    /// fire, firewood), a meal (table, seats, food), a scribe's desk (table, chair, candles, chest), stores (crates
    /// stacked, barrels), an armoury (weapon and armour stands), an altar (statue, candles, offerings), bones
    /// (a stack, skulls, a long bone), a workshop (bench and tools). Whatever has no room is left out.
    /// </summary>
    private void Cluster(Frame f, string kind, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (fu, fv) = In(at, 1.5f);
        var (lu, lv) = Beside(at, -1.1f);
        var (ru, rv) = Beside(at, 1.1f);
        var (table, seat, bench) = Furniture;
        switch (kind)
        {
            case "camp":
                Put(f, One("Morkhalla_Bedroll1", "Morkhalla_Bedroll2"), u, v, yaw + 90 + R(-10, 10), 0.8f);
                Put(f, One("Morkhalla_firepit", "fire_pit"), fu, fv, R(0, 360), 1.0f);
                Put(f, One("prop_wood_stack", "piece_pot3_cracked", "barrell"), ru, rv, R(0, 360), 0.5f);
                break;
            case "meal":
                if (Put(f, table, u, v, yaw + 90, 1.1f))
                {
                    Put(f, Chance(0.5) ? FeastFood : "prop_Tankard", u, v, R(0, 360), 0, y: 0.82f, force: true);
                    var (bu, bv) = In(at, 1.1f);
                    var (ku, kv) = In(at, -1.0f);
                    Put(f, bench == table ? seat : bench, bu, bv, yaw + 90, 0.4f);
                    Put(f, seat, ku, kv, yaw, 0.3f);
                }
                break;
            case "scribe":
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
                break;
            case "stores":
                string crate = _mat.Name == "Dvergr" ? "dvergrprops_crate" : "CargoCrate";
                string barrel = _mat.Name == "Dvergr" ? "dvergrprops_barrel" : "barrell";
                if (Put(f, crate, u, v, yaw + R(-12, 12), 0.6f) && Chance(0.6))
                {
                    _o.Add(crate, f.W(u, v, f.Top + Lift + 0.95f), f.Yaw(yaw + R(-25, 25)));
                }
                Put(f, barrel, lu, lv, R(0, 360), 0.5f);
                Put(f, Chance(0.5) ? barrel : crate, ru, rv, R(0, 360), 0.5f);
                Put(f, One("prop_wood_stack", "piece_pot1_cracked", "piece_chest_wood"), fu, fv, R(0, 360), 0.5f);
                break;
            case "armoury":
                Put(f, "Morkhalla_WeaponStand", u, v, yaw, 0.7f);
                Put(f, One("ArmorStand_Male", "ArmorStand_Female"), lu, lv, yaw + R(-8, 8), 0.5f);
                Put(f, One("ArmorStand_Male", "piece_chest_wood", "barrell"), ru, rv, yaw + R(-8, 8), 0.5f);
                break;
            case "altar":
                string idol = _style.Name switch
                {
                    "Temple" => One("StatueFreya", "StatueThor", "StatueSeed"),
                    "Goblin warren" => "goblin_totempole",
                    "Dvergr hold" => "blackmarble_altar_crystal",
                    _ => One("StatueEvil", "StatueDeer", "MountainGraveStone01"),
                };
                if (Put(f, idol, u, v, yaw, 0.7f))
                {
                    if (!_s.Respawn)
                    {
                        Put(f, "Candle_resin", lu, lv, R(0, 360), 0.2f);
                        Put(f, "Candle_resin", ru, rv, R(0, 360), 0.2f);
                    }
                    else
                    {
                        Put(f, "skull_pile", lu, lv, R(0, 360), 0.3f);
                    }
                    var (ou, ov) = In(at, 1.0f);
                    Put(f, Chance(0.5) ? _b.Pickable : "treasure_stack", ou, ov, R(0, 360), 0.3f);
                }
                break;
            case "bones":
                Put(f, "bone_stack", u, v, yaw + R(-30, 30), 0.5f);
                Put(f, "skull_pile", lu, lv, R(0, 360), 0.4f);
                Put(f, One("LargeBone", "Skull1", "skull_pile"), ru, rv, R(0, 360), 0.4f);
                Put(f, One("Skull1", "LargeBone_half01", "Pickable_ForestCryptRemains02"), fu, fv, R(0, 360), 0.3f);
                break;
            case "workshop":
                if (Put(f, _mat.Name == "Dvergr" ? "dvergrprops_table" : "piece_workbench", u, v, yaw + 90, 1.0f))
                {
                    Put(f, One("prop_piece_workbench_ext1", "prop_piece_workbench_ext2"), lu, lv, yaw, 0.6f);
                    Put(f, One("prop_wood_stack", "dvergrprops_pickaxe", "prop_piece_workbench_ext4"), ru, rv, yaw, 0.6f);
                }
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Up to n clusters in the room's corners (the convex ones, away from doorways), each a kind from the list in
    /// turn: the secondary groups that make a room read as lived in, beside its one main feature.
    /// </summary>
    private void Clusters(Frame f, int n, params string[] kinds)
    {
        int k = 0;
        foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex).OrderBy(_ => _rnd.Next()))
        {
            if (k >= n)
            {
                break;
            }
            var (cu, cv) = In((u, v, yaw), 1.3f);
            if (f.NearDoor(cu, cv, 2.4f) || !f.Free(cu, cv, 0.9f))
            {
                continue;
            }
            Cluster(f, kinds[k % kinds.Length], cu, cv, yaw);
            k++;
        }
    }
}

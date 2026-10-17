namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>Stores: crates stacked, barrels beside them, a wood stack, sometimes a crate left open.</summary>
    private void ClusterStores(Frame f, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (fu, fv) = In(at, 1.5f);
        var (lu, lv) = Beside(at, -1.1f);
        var (ru, rv) = Beside(at, 1.1f);
        string crate = _mat.Name == "Dvergr" ? "dvergrprops_crate" : "CargoCrate";
        string barrel = _mat.Name == "Dvergr" ? "dvergrprops_barrel" : "barrell";
        if (Put(f, crate, u, v, yaw + R(-12, 12), 0.6f) && Chance(0.6))
        {
            _o.Add(crate, f.W(u, v, f.Top + Lift + 0.95f), f.Yaw(yaw + R(-25, 25)));
        }
        Put(f, barrel, lu, lv, R(0, 360), 0.5f);
        Put(f, Chance(0.5) ? barrel : crate, ru, rv, R(0, 360), 0.5f);
        Put(f, One("prop_wood_stack", "piece_pot1_cracked", "piece_chest_wood"), fu, fv, R(0, 360), 0.5f);
    }

    /// <summary>An armoury: a weapon stand at the front, armour stands and a chest or barrel either side.</summary>
    private void ClusterArmoury(Frame f, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (lu, lv) = Beside(at, -1.1f);
        var (ru, rv) = Beside(at, 1.1f);
        Put(f, "Morkhalla_WeaponStand", u, v, yaw, 0.7f);
        Put(f, One("ArmorStand_Male", "ArmorStand_Female"), lu, lv, yaw + R(-8, 8), 0.5f);
        Put(f, One("ArmorStand_Male", "piece_chest_wood", "barrell"), ru, rv, yaw + R(-8, 8), 0.5f);
    }

    /// <summary>An altar: an idol with candles or a skull either side, and an offering in front.</summary>
    private void ClusterAltar(Frame f, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (lu, lv) = Beside(at, -1.1f);
        var (ru, rv) = Beside(at, 1.1f);
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
    }

    /// <summary>Bones: a stack in front, skulls and a long bone beside, a skull pile at the foot.</summary>
    private void ClusterBones(Frame f, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (fu, fv) = In(at, 1.5f);
        var (lu, lv) = Beside(at, -1.1f);
        var (ru, rv) = Beside(at, 1.1f);
        Put(f, "bone_stack", u, v, yaw + R(-30, 30), 0.5f);
        Put(f, "skull_pile", lu, lv, R(0, 360), 0.4f);
        Put(f, One("LargeBone", "Skull1", "skull_pile"), ru, rv, R(0, 360), 0.4f);
        Put(f, One("Skull1", "LargeBone_half01", "Pickable_ForestCryptRemains02"), fu, fv, R(0, 360), 0.3f);
    }
}

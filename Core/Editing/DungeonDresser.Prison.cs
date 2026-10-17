namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{

    /// <summary>A torture chamber: a butcher's table, chains on the walls, bones about.</summary>
    private void Torture(Frame f)
    {
        Put(f, "prop_cauldron_ext3_butchertable", 0, 0, R(0, 360), 1.1f);
        Brazier(f, 1.8f, 1.2f);
        Put(f, "prop_piece_workbench_ext3", -(f.HalfA - 0.6f), 0, 90, 1f);
        foreach (var s in f.WallSpots(2.4f, 3).OrderBy(_ => _rnd.Next()).Take(4))
        {
            Hang(f, "Morkhalla_WallChain1", s);
        }
        if (f.S.Tall)
        {
            _o.Add("dvergrprops_hooknchain", f.W(-1.5f, -1.5f, f.Top + f.H - 2.5f), f.Yaw(0));
        }
        Clutter(f, 0.04f, "Skull1", "LargeBone", "Pickable_ForestCryptRemains01", "skull_pile");
        Lights();
        Foes(f, 0.7f);
        Decay();
    }

    /// <summary>A guard room: a table with stools and armour stands or barrels; or a drill room with weapon stands, a dummy and a target. An elite guard.</summary>
    private void Guard(Frame f)
    {
        if (Chance(0.5))
        {
            Put(f, "dvergrprops_table", 0, 0, 90, 1f);
            Put(f, "dvergrprops_stool", -1, 0, 90, 0.3f);
            Put(f, "dvergrprops_stool", 1, 0, -90, 0.3f);
            Put(f, "prop_Tankard", 0, 0.2f, 0, 0, y: 0.78f, force: true);
            foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(2))
            {
                var (u, v) = In(s, 0.5f);
                Put(f, Chance(0.5) ? "ArmorStand_Male" : "barrell", u, v, s.Yaw, 0.6f);
            }
        }
        else
        {
            Put(f, "Morkhalla_WeaponStand", 0, f.HalfD - 0.8f, 180, 0.8f);
            Put(f, One("Morkhalla_Trainingdummy1", "Morkhalla_Trainingdummy2"), -1.5f, 0.5f, R(0, 360), 0.6f);
            Put(f, "piece_ArcheryTarget", f.HalfA - 0.8f, 0, -90, 0.6f);
            Put(f, "piece_logbench01", -(f.HalfA - 0.6f), -1, 90, 0.6f);
            Put(f, "ArmorStand_Male", -(f.HalfA - 0.8f), f.HalfD - 1.2f, 135, 0.6f);
        }
        Clusters(f, 1, "meal", "armoury");
        Lights(important: true);
        Elite(f, 0, f.HalfD - 1.6f, 180);
        Foes(f, 1f);
        Decay();
    }

    /// <summary>The treasury: treasure piles and stacks, metal bars, chests, braziers in the corners.</summary>
    private void Treasury(Frame f)
    {
        float loot = Math.Max(0.5f, _s.Loot) * Depth(f);
        for (int i = 0, times = Count(1 + loot); i < times; i++)
        {
            PutNear(f, "treasure_pile", R(-1.5f, 1.5f), R(-0.5f, 1.5f), R(0, 360), 1.2f, 1.5f);
        }
        for (int i = 0, times = Count(3 * loot); i < times; i++)
        {
            PutNear(f, "treasure_stack", R(-2, 2), R(-1, 2), R(0, 360), 0.3f, 2f);
        }
        for (int i = 0, times = Count(2 * loot); i < times; i++)
        {
            PutNear(f, _b.Bars, R(-2.5f, 2.5f), f.HalfD - 1.2f, R(-10, 10), 0.5f, 1f);
        }
        foreach (var s in f.WallSpots(2.5f, 3).Where(s => s.Yaw != 0).OrderBy(_ => _rnd.Next()).Take(Count(1.5f + loot)))
        {
            var (u, v) = In(s, 0.6f);
            Chest(f, u, v, s.Yaw, force: true);
        }
        Cores(f, Count(1 + loot));
        WallGems(f, Count(1 + loot));
        foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex))
        {
            var (bu, bv) = In((u, v, yaw), 1f);
            Brazier(f, bu, bv);
        }
        Pickables(f, 2);
    }
}

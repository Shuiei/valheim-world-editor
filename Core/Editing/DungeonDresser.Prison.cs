
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A block of cells either side of an aisle 4 m wide, each 4 m along it behind a grate.</summary>
    private void Cells(Frame f)
    {
        float aisle = 2;
        int n = (int)MathF.Floor((2 * f.HalfD) / 4);
        float v0 = -n * 2f, end = f.HalfD + _mat.Thick / 2;
        for (int i = 0; i < n; i++)
        {
            float a = v0 + 4 * i, b = a + 4;
            foreach (int side in new[] { -1, 1 })
            {
                float front = side * aisle, back = side * f.HalfA;
                if (i > 0)
                {
                    InnerWall(f, Math.Min(front, back), a, Math.Max(front, back), a, 0, 4);
                }
                // The first and last cells' fronts run on to the end walls' middle: the cells take 4 m each, and the
                // strip the block leaves at each end was open to the aisle.
                InnerWall(f, front, i == 0 ? -end : a, front, a + 1, 0, 4);
                InnerWall(f, front, b - 1, front, i == n - 1 ? end : b, 0, 4);
                InnerWall(f, front, a + 1, front, b - 1, 3, 4);
                _o.Add("iron_grate", f.W(front, a + 2, f.F + 1), f.Yaw(90));
                float mid = (front + back) / 2;
                Put(f, Chance(0.5) ? "goblin_strawpile" : "rug_straw", mid, (a + b) / 2, R(0, 360), 0, force: true);
                Hang(f, "Morkhalla_WallChain1", (back, (a + b) / 2, side < 0 ? 90 : -90));
                if (Chance(0.6))
                {
                    _o.Add(One("Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Skull1"), f.W(mid + side * 0.8f, a + R(1, 3), f.Top + Lift), f.Yaw(R(0, 360)));
                }
                else if (Chance(0.3) && _s.Loot > 0)
                {
                    _o.Add(_b.Pickable, f.W(mid, a + 2, f.Top + Lift), f.Yaw(R(0, 360)));
                }
                else if (Chance(0.4) && _s.Monsters > 0)
                {
                    var p = f.W(mid, a + 2, f.Top + Lift);
                    var foe = One(_foes);
                    _o.Add(_s.Respawn ? foe.Spawner : foe.Creature, p, f.Yaw(side < 0 ? 90 : -90));
                }
            }
        }
        // The aisle's walls are the cells': only the aisle is free. Each row of cells is kept by circles a metre apart
        // along its middle, each just wide enough that together they cover the row up to its grates.
        float half = (f.HalfA - aisle) / 2, keep = MathF.Sqrt(half * half + 0.25f);
        foreach (int side in new[] { -1, 1 })
        {
            for (float v = v0 + 0.5f; v < -v0; v++)
            {
                f.Take(side * (aisle + half), v, keep);
            }
        }
        for (float v = v0 + 2; v < -v0; v += 8)
        {
            Put(f, _b.Torch, aisle - 0.4f, v, -90, 0.3f);
        }
        Foes(f, 0.3f, _elites.Length > 0 ? _elites : null);
    }

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

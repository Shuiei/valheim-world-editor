
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A great hall, one of several: colonnades with a set piece at the far end; a mead hall round a fire; a hall of
    /// the dead with a sarcophagus on a dais between the columns; a bare echoing hall with its pillars fallen.
    /// </summary>
    private void Hall(Frame f)
    {
        bool cols = f.HalfA >= 5;
        int variant = _rnd.Next(cols ? 4 : 2);
        switch (variant)
        {
            case 0:
                // Colonnades, a feature at the far end, braziers and banners between the columns.
                if (Chance(0.5))
                {
                    Loft(f);
                }
                if (cols)
                {
                    for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
                    {
                        Pillar(f, -(f.HalfA - 2.2f), v);
                        Pillar(f, f.HalfA - 2.2f, v);
                    }
                }
                Feature(f, f.HalfD - 2.2f);
                Runner(f, -f.HalfD + 1.5f, f.HalfD - 4.2f);
                for (float v = -f.HalfD + 5; v <= f.HalfD - 5; v += 8)
                {
                    Brazier(f, -(f.HalfA - 1.2f), v);
                    Brazier(f, f.HalfA - 1.2f, v);
                }
                Hangings(f, (int)(f.HalfD / 3), Drapes);
                break;
            case 1:
                // A mead hall: a fire in the middle, benches round it, trestle tables along the sides, trophies.
                Put(f, One("prop_bonfire", "Morkhalla_firepit"), 0, -1, R(0, 360), 1.6f);
                foreach (int side in new[] { -1, 1 })
                {
                    Put(f, "piece_bench01", side * 2.6f, -1, side < 0 ? 90 : -90, 0.5f);
                    Put(f, "piece_logbench01", 0, -1 + side * 2.6f, side < 0 ? 0 : 180, 0.5f);
                }
                for (float v = 2.5f; v <= f.HalfD - 2.5f; v += 4)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        if (Put(f, "piece_table", side * (f.HalfA - 1.4f), v, 90, 1.1f))
                        {
                            Put(f, Chance(0.6) ? FeastFood : "prop_Tankard", side * (f.HalfA - 1.4f), v, R(0, 360), 0, y: 0.82f, force: true);
                        }
                    }
                }
                Trophies(f, 3);
                Hangings(f, (int)(f.HalfD / 3), Drapes);
                Pilasters(f, 8);
                break;
            case 2:
                // The hall of the dead: a dais at the far end bearing a sarcophagus between two statues, columns.
                if (Chance(0.4))
                {
                    Loft(f);
                }
                Plinth(f, 0, f.HalfD - 3, 6, 4);
                Steps(f, 0, f.HalfD - 6, 180, 6);
                _o.Add("crypt_skeleton_chest", f.W(0, f.HalfD - 3, f.F + 1 + Lift * 2), f.Yaw(90));
                string[] statues = Statues();
                _o.Add(statues[0], f.W(-2.3f, f.HalfD - 2.4f, f.F + 1 + Lift * 2), f.Yaw(180));
                _o.Add(statues[1], f.W(2.3f, f.HalfD - 2.4f, f.F + 1 + Lift * 2), f.Yaw(180));
                for (float v = -f.HalfD + 3; v <= f.HalfD - 8; v += 4)
                {
                    Pillar(f, -(f.HalfA - 2.2f), v);
                    Pillar(f, f.HalfA - 2.2f, v);
                    Put(f, "skull_pile", -(f.HalfA - 0.7f), v + 2, R(0, 360), 0.5f);
                    Put(f, "skull_pile", f.HalfA - 0.7f, v + 2, R(0, 360), 0.5f);
                }
                Brazier(f, -3.4f, f.HalfD - 6.8f);
                Brazier(f, 3.4f, f.HalfD - 6.8f);
                Runner(f, -f.HalfD + 1.5f, f.HalfD - 7.2f);
                Hangings(f, 2);
                Chains(f, 2);
                break;
            default:
                // Bare and echoing: a few columns standing, some fallen, rubble, the far end's altar cracked.
                for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        if (Chance(0.6))
                        {
                            Column(f, side * (f.HalfA - 2.2f), v);
                        }
                        else if (Spot(f, 1.3f) is var (pu, pv))
                        {
                            FallenPillar(f, _mat.Column[0].Prefab == "stone_pillar" ? "stone_pillar" : "Morkhalla_Rubble4", pu, pv, R(0, 360), 1.3f);
                        }
                    }
                }
                Put(f, _b.Name == "Ashlands" ? "blackmarble_altar_crystal_broken" : "StatueThor_broken_bottom", 0, f.HalfD - 2, 180, 1.4f);
                Clutter(f, 0.02f, "Morkhalla_Rubble2", "Morkhalla_Rubble1", "stone_wall_2x1_ruin", "Skull1", "LargeBone");
                Webs(f, 0.7f);
                break;
        }
        if (f.S.Tall)
        {
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add(_b.Name == "Ashlands" ? "CharredBanner1" : "cloth_hanging_long", f.W(side * 3f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
        }
        Clusters(f, 2, StyleClusters);
        Lights();
        Foes(f, 1.1f);
        Clutter(f, 0.012f);
        Decay();
    }
}

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A throne room: the dais with colonnades, armour stands and banners; or a war room with a long table and weapon
    /// stands before the dais; or the dais among fallen columns and broken statues.
    /// </summary>
    private void Throne(Frame f)
    {
        Dais(f, f.HalfD - 2);
        int variant = _rnd.Next(3);
        switch (variant)
        {
            case 0:
                if (Chance(0.5))
                {
                    Loft(f);
                }
                if (f.HalfA >= 5)
                {
                    for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
                    {
                        Pillar(f, -(f.HalfA - 2.4f), v);
                        Pillar(f, f.HalfA - 2.4f, v);
                    }
                }
                Runner(f, -f.HalfD + 1.2f, f.HalfD - 6.2f);
                for (float v = -f.HalfD + 4; v <= f.HalfD - 7; v += 4)
                {
                    Put(f, "ArmorStand_Male", -(f.HalfA - 0.8f), v, 90, 0.6f);
                    Put(f, "ArmorStand_Male", f.HalfA - 0.8f, v, -90, 0.6f);
                }
                break;
            case 1:
                // The long table, where it has the room: not across the dais' steps or a doorway's floor.
                float[] along = [-3f, -1.5f, 0f, 1.5f, 3f];
                if (along.All(dv => f.Free(0, -1 + dv, 0.8f)))
                {
                    _o.Add("piece_table_oak", f.W(0, -1, f.Top + Lift), f.Yaw(90));
                    foreach (float dv in along)
                    {
                        f.Take(0, -1 + dv, 0.8f);
                    }
                    Put(f, "piece_cartographytable", 0, -1, 0, 0, y: 0.82f, force: true);
                    foreach (int side in new[] { -1, 1 })
                    {
                        for (float dv = -2.4f; dv <= 2.41f; dv += 1.6f)
                        {
                            Put(f, "piece_chair03", side * 1.4f, -1 + dv, side < 0 ? 90 : -90, 0.4f);
                        }
                    }
                }
                foreach (var sp in f.WallSpots(4, 3).Where(x => x.Yaw is 90 or -90).OrderBy(_ => _rnd.Next()).Take(4))
                {
                    var (u, v) = In(sp, 0.7f);
                    Put(f, One("Morkhalla_WeaponStand", "ArmorStand_Male"), u, v, sp.Yaw, 0.7f);
                }
                Pilasters(f, 8);
                break;
            default:
                for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        if (Chance(0.5))
                        {
                            Column(f, side * (f.HalfA - 2.4f), v);
                        }
                        else if (_mat.Column[0].Prefab == "stone_pillar")
                        {
                            FallenPillar(f, "stone_pillar", side * (f.HalfA - 3.4f), v, R(60, 120), 1.4f);
                        }
                    }
                }
                Put(f, "StatueThor_broken_bottom", -3, f.HalfD - 7, R(0, 360), 1f);
                Put(f, "StatueFreya_broken_right", 3, -f.HalfD + 3, R(0, 360), 1f);
                Clutter(f, 0.02f, "Morkhalla_Rubble2", "Morkhalla_Rubble1", "stone_wall_1x1_ruin");
                Webs(f, 0.6f);
                break;
        }
        Hangings(f, 4, "piece_banner01", "piece_banner02", "piece_banner05", "piece_banner06", "piece_banner09", "piece_banner10");
        Trophies(f, 2);
        WallGems(f, 2);
        Clusters(f, 1, "armoury");
        Lights(important: true);
        Elite(f, -1.8f, f.HalfD - 6.5f, 180);
        Elite(f, 1.8f, f.HalfD - 6.5f, 180);
        Foes(f, 0.6f);
        Decay();
    }
}

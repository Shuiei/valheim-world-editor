
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A bed that suits the material.</summary>
    private string Bed => _mat.Name switch { "Goblin" => "goblin_bed", "Grausten" => "prop_ashwood_bed", _ => "dvergrprops_bed" };

    /// <summary>
    /// Barracks: beds along both walls with a table in the middle; or bedrolls round a fire with a weapon stand; or
    /// bunks in pairs with chests at their feet and the armour by the door.
    /// </summary>
    private void Barracks(Frame f)
    {
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (int side in new[] { -1, 1 })
                {
                    for (float v = -f.HalfD + 1.6f; v <= f.HalfD - 1; v += 1.9f)
                    {
                        float u = side * (f.HalfA - 1.15f);
                        if (f.NearDoor(u, v, 2.4f))
                        {
                            continue;
                        }
                        if (Put(f, Bed, u, v, (side < 0 ? 90 : -90) + R(-7, 7), 0.75f) && Chance(0.5))
                        {
                            Put(f, "dvergrprops_crate", side * (f.HalfA - 2.7f), v, 0, 0.5f);
                        }
                    }
                }
                Put(f, "dvergrprops_table", 0, 0, 0, 1.1f);
                foreach (var (u, v) in new[] { (-1.2f, 0f), (1.2f, 0f), (0f, -1f) })
                {
                    Put(f, "dvergrprops_stool", u, v, 0, 0.3f);
                }
                Put(f, "prop_Tankard", 0.3f, 0.1f, R(0, 360), 0, y: 0.78f, force: true);
                Put(f, "ArmorStand_Male", 0, f.HalfD - 0.7f, 180, 0.6f);
                break;
            case 1:
                Put(f, "Morkhalla_firepit", 0, 0, R(0, 360), 1.4f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3 + MathF.PI / 6;
                    PutNear(f, One("Morkhalla_Bedroll1", "Morkhalla_Bedroll2"), 2.8f * MathF.Cos(a), 2.8f * MathF.Sin(a), a * 180 / MathF.PI + 90, 0.8f, 0.5f);
                }
                Put(f, "Morkhalla_WeaponStand", 0, f.HalfD - 0.8f, 180, 0.8f);
                Put(f, "Morkhalla_Trainingdummy1", -(f.HalfA - 1.2f), f.HalfD - 1.5f, 135, 0.6f);
                Put(f, "prop_wood_stack", f.HalfA - 1.2f, f.HalfD - 1.3f, R(0, 360), 0.8f);
                break;
            default:
                foreach (int side in new[] { -1, 1 })
                {
                    for (float v = -f.HalfD + 2.4f; v <= f.HalfD - 1.5f; v += 3.2f)
                    {
                        float u = side * (f.HalfA - 1.15f);
                        if (f.NearDoor(u, v, 2.4f))
                        {
                            continue;
                        }
                        Put(f, Bed, u, v - 0.5f, side < 0 ? 90 : -90, 0.75f);
                        Put(f, Bed, u, v + 0.6f, side < 0 ? 90 : -90, 0.75f);
                        Put(f, "piece_chest_wood", side * (f.HalfA - 2.6f), v, side < 0 ? 90 : -90, 0.5f);
                    }
                }
                foreach (var m in f.Mouths.Take(1))
                {
                    var (mu, mv) = f.Local(m.X, m.Z);
                    var (su, sv) = (m.In.X * f.A.X + m.In.Z * f.A.Z, m.In.X * f.D.X + m.In.Z * f.D.Z);
                    Put(f, "ArmorStand_Male", mu + su * 1.2f - sv * 2.6f, mv + sv * 1.2f + su * 2.6f, 0, 0.6f);
                    Put(f, "ArmorStand_Female", mu + su * 1.2f + sv * 2.6f, mv + sv * 1.2f - su * 2.6f, 0, 0.6f);
                }
                Put(f, "piece_logbench01", 0, 0, 90, 0.6f);
                break;
        }
        Hangings(f, 1, Drapes);
        Clusters(f, 2, "stores", "armoury", "camp");
        Lights();
        Foes(f, 1.5f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>An armoury: armour stands along the walls with a work table; or weapon stands in rows with a target and a dummy. Trophies, a chest.</summary>
    private void Armory(Frame f)
    {
        if (Chance(0.5))
        {
            foreach (var s in f.WallSpots(1.6f, 2.8f))
            {
                var (u, v) = In(s, 0.6f);
                Put(f, Chance(0.6) ? "ArmorStand_Male" : "ArmorStand_Female", u, v, s.Yaw, 0.7f);
            }
            Put(f, "piece_table", 0, 0, 0, 1.3f);
            Put(f, "prop_piece_workbench_ext3", 0, 0, 0, 0, y: 0.82f, force: true);
        }
        else
        {
            foreach (var s in f.WallSpots(2.4f, 2.8f))
            {
                var (u, v) = In(s, 0.7f);
                Put(f, Chance(0.7) ? "Morkhalla_WeaponStand" : "ArmorStand_Male", u, v, s.Yaw, 0.8f);
            }
            Put(f, "piece_ArcheryTarget", 0, f.HalfD - 2.2f, 180, 0.7f);
            Put(f, One("Morkhalla_Trainingdummy1", "Morkhalla_Trainingdummy2"), 0, -0.5f, R(0, 360), 0.7f);
        }
        Trophies(f, 2);
        Chest(f, 0, -1.6f, 180);
        Lights();
        Foes(f, 0.6f);
        Decay();
    }

    /// <summary>The feast laid out on tables, the biome's where the game has one.</summary>
    private string FeastFood => _b.Name switch { "Ashlands" => "prop_FeastAshlands", "Deep North" => "PropFeastDeepNorth", _ => "prop_FeastMeadows" };
}

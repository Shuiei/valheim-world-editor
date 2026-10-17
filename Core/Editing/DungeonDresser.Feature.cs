
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>The set piece at the far end of a hall: an altar, a throne, a statue, a table.</summary>
    private void Feature(Frame f, float v)
    {
        switch (_style.Name)
        {
            case "Fortress" or "Goblin warren":
                Dais(f, v);
                break;
            case "Dvergr hold":
                Put(f, "dvergrprops_table", 0, v - 0.6f, 0, 1.2f);
                Put(f, "dvergrprops_chair", 0, v + 0.3f, 180, 0.4f);
                Put(f, "dvergrprops_lantern_standing", 0.5f, v - 0.6f, 0, 0, y: 0.78f, force: true);
                break;
            default:
                if (!(Chance(0.4) && PutHero(f, 0, v, 180)))
                {
                    Put(f, f.S.Tall ? "StatueEvil" : One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom"), 0, v, 180, 1.6f);
                }
                Brazier(f, -2.2f, v - 0.6f);
                Brazier(f, 2.2f, v - 0.6f);
                break;
        }
    }

    /// <summary>A throne that suits the material (a bone throne in a stone crypt); some are drawn, so call it once a seat.</summary>
    private string PickThrone() => _mat.Name switch
    {
        "Marble" or "Grausten" => "piece_blackmarble_throne",
        "Goblin" => "piece_bone_throne",
        "Stave" => "piece_moose_throne",
        _ => _style.Name == "Crypt" ? "piece_bone_throne" : One("piece_throne01", "piece_throne02"),
    };

    /// <summary>
    /// A dais of stone 1 m high across the far end, steps in front, the throne on it. It stands on the
    /// marble, through any wooden floor (stone on wood would not hold).
    /// </summary>
    private void Dais(Frame f, float v)
    {
        float back = Math.Min(f.HalfD - 0.6f, v + 1.2f);
        for (float u = -2; u <= 2.01f; u += 2)
        {
            for (float dv = 0; dv < 4; dv += 2)
            {
                _o.Add("stone_floor_2x2", f.W(u, back - 1 - dv, f.F + 0.5f + Lift), f.Yaw(0));
            }
        }
        for (float u = -2; u <= 2.01f; u += 2)
        {
            _o.Add("stone_stair", f.W(u, back - 5, f.F + Lift), f.Yaw(180));
        }
        f.Take(0, back - 2.5f, 3.6f);
        string throne = PickThrone();
        _o.Add(throne, f.W(0, back - 1.2f, f.F + 1 + Lift * 2), f.Yaw(180 + DungeonProps.Turn(throne)));
        Brazier(f, -3.6f, back - 1.5f);
        Brazier(f, 3.6f, back - 1.5f);
    }

    /// <summary>A crossing: a statue, a ring of braziers, a column or an altar in the middle; columns round it if large.</summary>
    private void Hub(Frame f)
    {
        switch (_rnd.Next(4))
        {
            case 0:
                if (!(Chance(0.35) && PutHero(f, 0, 0, 180, 1.8f)))
                {
                    Put(f, Statue("StatueThor", "StatueFreya", "StatueDeer", "StatueSeed"), 0, 0, 180, 1.4f);
                }
                break;
            case 1:
                for (int i = 0; i < 4; i++)
                {
                    float a = i * MathF.PI / 2 + MathF.PI / 4;
                    Brazier(f, 1.6f * MathF.Cos(a), 1.6f * MathF.Sin(a));
                }
                Put(f, "Morkhalla_giant_railing_deco", 0, 0, 0, 0.8f, y: 0.5f);
                break;
            case 2:
                Column(f, 0, 0);
                break;
            default:
                Put(f, "blackmarble_altar_crystal", 0, 0, 0, 1f, y: 0.9f);
                break;
        }
        if (f.S.Shape == DungeonPlan.Shape.Cross)
        {
            // Pilasters where the notches meet the walls.
            foreach (var (u, v, yaw, convex) in f.Corners().Where(c => !c.Convex))
            {
                var (cu, cv) = In((u, v, yaw), 0.5f);
                Column(f, cu, cv);
            }
        }
        else if (f.HalfA >= 5 && f.HalfD >= 5)
        {
            foreach (var (u, v) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
            {
                if (!f.NearDoor(u, v, 3))
                {
                    Column(f, u, v);
                }
            }
        }
        Hangings(f, 2, Drapes);
        Clusters(f, 1, StyleClusters);
        Lights();
        Foes(f, 0.6f);
        Clutter(f, 0.015f);
        Decay();
    }

    /// <summary>
    /// A gallery, one of several: the dead in niches between pilasters; sarcophagi standing along the walls; statues and
    /// braziers in turn; or bare, cobwebbed, with rubble.
    /// </summary>
    private void Gallery(Frame f)
    {
        string[] remains = [.. _b.Clutter.Where(c => c.Contains("Remains", StringComparison.Ordinal) || c == "Skull1" || c == "LargeBone")];
        if (remains.Length == 0)
        {
            remains = ["Skull1", "LargeBone"];
        }
        int variant = _rnd.Next(4);
        for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 2)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float u = side * (f.HalfA - 0.6f);
                if (f.NearDoor(u, v, 2.6f))
                {
                    continue;
                }
                int slot = (int)((v + f.HalfD) / 2) % 3;
                switch (variant)
                {
                    case 0 when slot == 0:
                        Column(f, side * (f.HalfA - 0.5f), v);
                        break;
                    case 0:
                        if (Chance(0.7))
                        {
                            Put(f, One(remains), u, v, side < 0 ? 90 : -90, 0.5f);
                        }
                        break;
                    case 1 when slot == 1:
                        Put(f, "crypt_skeleton_chest", side * (f.HalfA - 1.1f), v, side < 0 ? 0 : 180, 1.1f);
                        break;
                    case 1 when slot == 0:
                        Column(f, side * (f.HalfA - 0.5f), v);
                        break;
                    case 2 when slot == 1:
                        Put(f, One("StatueThor", "StatueFreya", "StatueDeer", "StatueEvil"), side * (f.HalfA - 0.9f), v, side < 0 ? 90 : -90, 0.8f);
                        break;
                    case 2 when slot == 0 && Chance(0.5):
                        Brazier(f, side * (f.HalfA - 1f), v);
                        break;
                    case 3 when slot == 0:
                        Put(f, One("Morkhalla_Rubble2", "skull_pile", "bone_stack"), u, v, R(0, 360), 0.5f);
                        break;
                    default:
                        break;
                }
            }
        }
        if (variant == 3)
        {
            Webs(f, 0.8f);
            Chains(f, 1);
        }
        Lights();
        Foes(f, 0.8f);
        Clutter(f, 0.01f);
        Decay();
    }
}

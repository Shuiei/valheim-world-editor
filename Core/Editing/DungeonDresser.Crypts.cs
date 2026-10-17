using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A burial chamber, one of several: rows of sarcophagi each side of an aisle; one tomb on a dais in the middle,
    /// candles and skulls about it; sarcophagi set at odd angles among the bones of a catacomb. Sleeping draugr in
    /// the swamp.
    /// </summary>
    private void Burial(Frame f)
    {
        switch (_rnd.Next(f.HalfA >= 3.4f && f.HalfD >= 3.4f ? 4 : 3))
        {
            case 3:
                // The vault: an inner chamber against the far wall holding the sarcophagus, the dead's goods about it.
                Vault(f, 4, 3.5f);
                _o.Add("crypt_skeleton_chest", f.W(0, f.HalfD - 1.75f, f.Top + Lift), f.Yaw(90));
                f.Take(0, f.HalfD - 1.75f, 1.3f);
                Put(f, One("piece_chest_wood", "barrell", "piece_pot2_cracked"), -1.2f, f.HalfD - 0.7f, R(0, 360), 0.3f, force: true);
                Put(f, "skull_pile", 1.3f, f.HalfD - 0.7f, R(0, 360), 0.3f, force: true);
                Brazier(f, -(f.HalfA - 1), -(f.HalfD - 1));
                Brazier(f, f.HalfA - 1, -(f.HalfD - 1));
                Clutter(f, 0.02f, "Skull1", "LargeBone", "bone_stack");
                break;
            case 0:
                float uRow = Math.Max(2.2f, f.HalfA / 2 + 0.3f);
                for (float v = -f.HalfD + 2.2f; v <= f.HalfD - 1.6f; v += 2.4f)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Put(f, "crypt_skeleton_chest", side * uRow, v, side < 0 ? 0 : 180, 1.2f);
                    }
                }
                break;
            case 1:
                Plinth(f, 0, 0.5f, 4, 4);
                _o.Add("crypt_skeleton_chest", f.W(0, 0.5f, f.F + 1 + Lift * 2), f.Yaw(90));
                Steps(f, 0, -2.5f, 180, 2);
                foreach (var (du, dv) in new[] { (-2.6f, -2.6f), (2.6f, -2.6f), (-2.6f, 3.6f), (2.6f, 3.6f) })
                {
                    Put(f, "skull_pile", du, dv, R(0, 360), 0.5f);
                }
                if (!_s.Respawn)
                {
                    foreach (var (du, dv) in new[] { (-1.6f, -1.6f), (1.6f, -1.6f), (-1.6f, 2.6f), (1.6f, 2.6f) })
                    {
                        _o.Add("Candle_resin", f.W(du, dv, f.F + 1 + Lift * 2), f.Yaw(R(0, 360)));
                    }
                }
                Brazier(f, -(f.HalfA - 1), f.HalfD - 1);
                Brazier(f, f.HalfA - 1, f.HalfD - 1);
                Chains(f, 2);
                break;
            default:
                for (int i = 0, times = Count(f.Area / 14); i < times; i++)
                {
                    if (Spot(f, 1.2f, 0.6f) is var (u, v))
                    {
                        Put(f, "crypt_skeleton_chest", u, v, R(0, 360), 1.2f);
                    }
                }
                Clutter(f, 0.03f, "Skull1", "LargeBone", "bone_stack", "skull_pile");
                Webs(f, 0.6f);
                break;
        }
        foreach (var (cu, cv) in new[] { (-1, 1), (1, 1) })
        {
            Put(f, "skull_pile", cu * (f.HalfA - 0.7f), cv * (f.HalfD - 0.7f), R(0, 360), 0.6f);
        }
        Clusters(f, 1, "bones", "altar");
        Lights();
        Hangings(f, 1);
        // The dead do not rest here.
        var sleepers = _b.Name == "Swamp" && _s.Foes is not { Count: > 0 } ? new[] { new Foe("Spawner_Draugr", "Draugr_sleeping"), new Foe("Spawner_Draugr_Ranged", "Draugr_Ranged_sleeping") } : null;
        Foes(f, 1.1f, sleepers);
        Clutter(f, 0.02f);
        Decay();
    }

    /// <summary>
    /// An ossuary: bones along every wall, or a great heap in the middle with skulls in the corners, or stacked in rows
    /// forming aisles; with monsters, a bone pile that raises the dead.
    /// </summary>
    private void Ossuary(Frame f)
    {
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (var s in f.WallSpots(1.25f, 2.6f))
                {
                    var (u, v) = In(s, 0.55f);
                    Put(f, Chance(0.5) ? "skull_pile" : "bone_stack", u, v, s.Yaw + R(-20, 20), 0.5f);
                }
                break;
            case 1:
                for (int i = 0; i < 7; i++)
                {
                    PutNear(f, One("bone_stack", "skull_pile", "LargeBone"), R(-1.5f, 1.5f), R(-1.5f, 1.5f), R(0, 360), 0.35f, 1f);
                }
                foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex))
                {
                    var (cu, cv) = In((u, v, yaw), 0.9f);
                    Put(f, "skull_pile", cu, cv, R(0, 360), 0.5f);
                }
                Webs(f, 0.7f);
                break;
            default:
                for (float u = -f.HalfA + 1.5f; u <= f.HalfA - 1.5f; u += 3)
                {
                    for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 1.3f)
                    {
                        Put(f, Chance(0.8) ? "bone_stack" : "skull_pile", u, v, 90, 0.5f);
                    }
                }
                break;
        }
        if (_s.Monsters > 0 && _s.Respawn && _s.Foes is not { Count: > 0 })
        {
            // A heap of bones that keeps raising the dead until broken (the biome's: none when foes are chosen, nor when
            // nothing respawns).
            Put(f, _b.Name == "Swamp" ? "Spawner_DraugrPile" : _b.Name is "Black Forest" or "Meadows" ? "BonePileSpawner" : "BonePileSpawner_swamp", 0, R(-1, 1), R(0, 360), 1.3f);
        }
        Clutter(f, 0.04f, "Skull1", "LargeBone", "LargeBone_half01", "Skull1");
        Clusters(f, 1, "bones");
        Lights();
        Foes(f, 0.4f);
        Decay();
    }

    /// <summary>
    /// A chapel: an altar between two statues at the far end with pews facing it; or a round shrine, the altar in the
    /// middle in a ring of braziers; or a bare nave of rugs and candles under long hangings.
    /// </summary>
    private void Chapel(Frame f)
    {
        float far = f.HalfD - 1.6f;
        string altar = One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom");
        string[] statues = Statues();
        string bench = _mat.Name is "Marble" or "Grausten" ? "piece_blackmarble_bench" : _mat.Name == "Stave" ? "prop_piece_bench_runed" : "piece_bench01";
        int variant = _rnd.Next(3);
        switch (variant)
        {
            case 0:
                if (Chance(0.4))
                {
                    Loft(f);
                }
                Put(f, altar, 0, far, 180, 1.4f);
                Put(f, statues[0], -2.6f, far + 0.3f, 180, 0.6f);
                Put(f, statues[1], 2.6f, far + 0.3f, 180, 0.6f);
                Brazier(f, -1.6f, far - 1.8f);
                Brazier(f, 1.6f, far - 1.8f);
                for (float v = -f.HalfD + 3; v <= far - 3.5f; v += 1.7f)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        for (float u = 2.6f; u <= f.HalfA - 1.2f; u += 2.6f)
                        {
                            Put(f, bench, side * u, v, 0, 0.7f);
                        }
                    }
                }
                Runner(f, -f.HalfD + 1.2f, far - 1.4f);
                break;
            case 1:
                Plinth(f, 0, 0.5f, 4, 4);
                _o.Add(altar, f.W(0, 0.5f, f.F + 1 + Lift * 2), f.Yaw(180));
                Steps(f, 0, -2.5f, 180, 2);
                float r = Math.Min(f.HalfA, f.HalfD) - 1.4f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3 + MathF.PI / 6;
                    Brazier(f, r * MathF.Cos(a), 0.5f + r * MathF.Sin(a));
                }
                Put(f, statues[0], -(f.HalfA - 1), far + 0.3f, 180, 0.6f);
                Put(f, statues[1], f.HalfA - 1, far + 0.3f, 180, 0.6f);
                Pilasters(f, 4);
                break;
            default:
                Put(f, altar, 0, far, 180, 1.4f);
                Put(f, "StatueEvil", 0, far - 2.4f, 180, 1f);
                for (float v = -f.HalfD + 2.5f; v <= far - 4; v += 3)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Put(f, One("jute_carpet", "jute_carpet_blue", "rug_fur"), side * 2.2f, v, 0, 0, force: true);
                    }
                }
                if (!_s.Respawn)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (Spot(f, 0.2f, 0.5f) is var (cu, cv))
                        {
                            Put(f, "Candle_resin", cu, cv, R(0, 360), 0.2f);
                        }
                    }
                }
                Chains(f, 2);
                break;
        }
        Pickables(f, 0.5f);
        if (f.S.Tall)
        {
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add("cloth_hanging_long", f.W(side * 2.2f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
        }
        Hangings(f, 2, Drapes);
        Clusters(f, 1, "altar");
        Lights(important: true);
        Foes(f, 0.7f);
        Clutter(f, 0.008f);
        Decay();
    }
}

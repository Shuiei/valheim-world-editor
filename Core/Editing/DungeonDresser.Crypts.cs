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
}

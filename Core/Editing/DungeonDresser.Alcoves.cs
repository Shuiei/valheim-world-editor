
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A point d metres along the wall from a spot facing yaw (to its right for d &gt; 0).</summary>
    private static (float U, float V) Beside((float U, float V, float Yaw) s, float d) =>
        (s.U + d * MathF.Cos(s.Yaw * MathF.PI / 180), s.V - d * MathF.Sin(s.Yaw * MathF.PI / 180));

    /// <summary>
    /// A wide alcove's furniture, by the style: a sarcophagus across it with a skull beside, a statue between two
    /// candles, a bed in its bay with a chest at its foot, a shelf and crates, a totem on straw. False when the
    /// style's choice has no place there (the single nook's piece is then tried).
    /// </summary>
    /// <summary>Two candles (skulls when the dungeon respawns) either side of a spot, a bit out from it.</summary>
    private void FlankingCandles(Frame f, (float U, float V, float Yaw) spot)
    {
        foreach (int side in new[] { -1, 1 })
        {
            var (cu, cv) = Beside(spot, side * 1.1f);
            Put(f, _s.Respawn ? "skull_pile" : "Candle_resin", cu, cv, R(0, 360), 0.25f);
        }
    }

    /// <summary>Furnishes an alcove in the frame's wall (see <see cref="Alcove"/> for the rules).</summary>
    private bool Alcove(Frame f, float u, float v, float yaw)
    {
        // An alcove is a tile deep less its wall: the big piece stands at its middle, a touch out, and is not refused
        // for being near the walls (the planner keeps alcoves clear of doorways and other rooms).
        var spot = (u, v, yaw);
        var (bu, bv) = In(spot, 0.15f);
        switch (_style.Name)
        {
            case "Crypt" or "Catacombs" or "Ruins":
                switch (_rnd.Next(3))
                {
                    case 0:
                        if (!Put(f, "crypt_skeleton_chest", bu, bv, yaw + 90, 0.8f, force: true))
                        {
                            return false;
                        }
                        var (su, sv) = Beside(spot, 1.4f);
                        Put(f, One("skull_pile", "Skull1", "Candle_resin"), su, sv, R(0, 360), 0.3f);
                        return true;
                    case 1:
                        if (!Put(f, Statue("StatueEvil", "StatueThor", "StatueFreya"), bu, bv, yaw, 0.7f, force: true))
                        {
                            return false;
                        }
                        FlankingCandles(f, spot);
                        return true;
                    default:
                        foreach (int side in new[] { -1, 1 })
                        {
                            var (ou, ov) = Beside((bu, bv, yaw), side * 0.9f);
                            Put(f, One("bone_stack", "skull_pile", "LargeBone"), ou, ov, R(0, 360), 0.4f);
                        }
                        return true;
                }
            case "Temple":
                if (!Put(f, One("StatueFreya", "StatueThor", "StatueSeed"), bu, bv, yaw, 0.7f, force: true))
                {
                    return false;
                }
                FlankingCandles(f, spot);
                return true;
            case "Fortress" or "Prison":
                if (Chance(0.5))
                {
                    // A sleeping bay: a bed along the back, a chest at its foot.
                    if (!Put(f, "bed", bu, bv, yaw + 90, 0.9f, force: true))
                    {
                        return false;
                    }
                    var (cu, cv) = Beside((u, v, yaw), 1.5f);
                    Put(f, "piece_chest_wood", cu, cv, yaw, 0.4f);
                    return true;
                }
                foreach (int side in new[] { -1, 1 })
                {
                    var (ou, ov) = Beside((bu, bv, yaw), side * 0.9f);
                    Put(f, side < 0 ? "Morkhalla_WeaponStand" : One("ArmorStand_Male", "ArmorStand_Female"), ou, ov, yaw, 0.5f);
                }
                return true;
            case "Dvergr hold":
                if (!Put(f, Chance(0.5) ? "dvergrprops_shelf" : "dvergrprops_bed", bu, bv, yaw + (Chance(0.5) ? 0 : 90), 0.8f, force: true))
                {
                    return false;
                }
                var (ku, kv) = Beside(spot, 1.3f);
                Put(f, One("dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_lantern_standing"), ku, kv, R(0, 360), 0.4f);
                return true;
            case "Goblin warren":
                Put(f, "goblin_strawpile", u, v, R(0, 360), 0, force: true);
                return Put(f, One("goblin_totempole", "goblin_bed", "goblin_trashpile"), bu, bv, yaw, 0.7f, force: true);
            default:
                return false;
        }
    }

    /// <summary>
    /// What stands in the room's alcoves and niches, facing out: in crypts a sarcophagus, bones or a statue; in a
    /// temple a statue with a candle; in a keep or a prison an armour or weapon stand or a barrel; in a hold crates
    /// and a lantern; in a warren a totem or straw. A brazier now and then, whatever the style. A wide alcove gets
    /// the bigger things (a sarcophagus across it, a bench, a pair of statues) with a candle or a skull beside.
    /// </summary>
    private void Nooks(Frame f)
    {
        foreach (var (u, v, yaw, width) in f.Nooks())
        {
            // A corridor's niche hides a foe now and then: the ambush from the blind spot.
            if (f.S.Corridor && _s.Monsters > 0 && _foes.Length > 0 && Chance(0.25))
            {
                Spawn(f, One(_foes), u, v, yaw, force: true);
                continue;
            }
            if (Chance(0.2))
            {
                Brazier(f, u, v);
                continue;
            }
            if (width >= 2 && Alcove(f, u, v, yaw))
            {
                continue;
            }
            switch (_style.Name)
            {
                case "Crypt" or "Catacombs" or "Ruins":
                    Put(f, One("crypt_skeleton_chest", "crypt_skeleton_chest", "skull_pile", "bone_stack", "StatueEvil", "StatueDeer"), u, v, yaw + 90, 0.6f);
                    break;
                case "Temple":
                    if (Put(f, One("StatueFreya", "StatueThor", "StatueSeed"), u, v, yaw, 0.6f) && !_s.Respawn)
                    {
                        _o.Add("Candle_resin", f.W(u + 0.6f, v + 0.4f, f.Top + Lift), f.Yaw(R(0, 360)));
                    }
                    break;
                case "Fortress" or "Prison":
                    Put(f, One("ArmorStand_Male", "Morkhalla_WeaponStand", "barrell", "piece_chest_wood"), u, v, yaw, 0.6f);
                    break;
                case "Dvergr hold":
                    Put(f, One("dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_lantern_standing", "dvergrprops_crate_long"), u, v, yaw, 0.6f);
                    break;
                case "Goblin warren":
                    Put(f, One("goblin_totempole", "goblin_strawpile", "skull_pile", "goblin_trashpile"), u, v, yaw, 0.6f);
                    break;
                default:
                    Put(f, One("skull_pile", "barrell", "StatueDeer"), u, v, yaw, 0.6f);
                    break;
            }
        }
    }
}

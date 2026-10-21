
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
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

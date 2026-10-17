namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
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

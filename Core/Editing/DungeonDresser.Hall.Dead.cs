namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// The hall of the dead: a dais at the far end bearing a sarcophagus between two statues, columns lined with skulls
    /// and bones, braziers, banners and chains.
    /// </summary>
    private void HallDead(Frame f)
    {
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
    }

    /// <summary>
    /// A bare, echoing hall: a few columns standing and some fallen, rubble, the far end's altar cracked, cobwebs.
    /// </summary>
    private void HallBare(Frame f)
    {
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
    }
}

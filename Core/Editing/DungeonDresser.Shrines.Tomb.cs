namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A tomb: a sarcophagus and grave stone among four columns; or on a dais between two statues, chains above;
    /// treasure, an elite guard.
    /// </summary>
    private void Tomb(Frame f)
    {
        if (Chance(0.5))
        {
            Put(f, "crypt_skeleton_chest", 0, 0.5f, 90, 1.6f);
            Put(f, "MountainGraveStone01", 0, 2.6f, 180, 0.6f);
            foreach (var (u, v) in new[] { (-2.3f, -1.6f), (2.3f, -1.6f), (-2.3f, 2.6f), (2.3f, 2.6f) })
            {
                Column(f, u, v);
            }
        }
        else
        {
            Plinth(f, 0, 1, 4, 4);
            _o.Add("crypt_skeleton_chest", f.W(0, 1, f.F + 1 + Lift * 2), f.Yaw(90));
            Steps(f, 0, -2, 180, 2);
            string[] statues = Statues();
            Put(f, statues[0], -(f.HalfA - 1), f.HalfD - 1, 180, 0.6f);
            Put(f, statues[1], f.HalfA - 1, f.HalfD - 1, 180, 0.6f);
            Chains(f, 2);
        }
        Brazier(f, -f.HalfA + 1, -f.HalfD + 1);
        Brazier(f, f.HalfA - 1, -f.HalfD + 1);
        for (int i = 0, times = Count(1 + _s.Loot); i < times; i++)
        {
            PutNear(f, "treasure_stack", R(-1, 1), -1.2f, R(0, 360), 0.3f, 0.8f);
        }
        Hangings(f, 2);
        Elite(f, 0, -0.8f, 180);
        Clutter(f, 0.015f);
        Decay();
    }
}

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// Rubble in the corners, broken walls and pots, now and then a fallen pillar: more the more decayed. After
    /// everything else, from a stream of the room's own, so that the settings' Decay changes nothing but the decay.
    /// </summary>
    private void PlaceDecay(Frame f)
    {
        if (_decay <= 0)
        {
            return;
        }
        var rnd = Local(f, 2);
        float Rr(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        int Cc(float n) => n <= 0 ? 0 : (int)MathF.Floor(n) + (rnd.NextDouble() < n - MathF.Floor(n) ? 1 : 0);
        string[] rubbles = ["Morkhalla_Rubble2", "Morkhalla_Rubble1", "Morkhalla_Rubble3", "Morkhalla_Rubble1"];
        string[] debrisKinds = ["stone_wall_2x1_ruin", "stone_wall_1x1_ruin", "piece_pot1_cracked", "piece_pot3_cracked"];
        int rubble = Cc(f.Area / 90 * _decay * 2);
        var corners = f.Corners().Where(c => c.Convex).OrderBy(_ => rnd.Next()).ToList();
        for (int i = 0; i < rubble && corners.Count > 0; i++)
        {
            var (u, v, yaw, _) = corners[i % corners.Count];
            var (cu, cv) = In((u, v, yaw), 2.2f);
            for (int t = 0; t < 8 && !Put(f, rubbles[rnd.Next(rubbles.Length)], cu + (t == 0 ? 0 : Rr(-0.6f, 0.6f)), cv + (t == 0 ? 0 : Rr(-0.6f, 0.6f)), Rr(0, 360), 1.8f); t++)
            {
            }
        }
        int debris = Cc(f.Area / 40 * _decay * 2);
        for (int i = 0; i < debris; i++)
        {
            for (int t = 0; t < 30; t++)
            {
                float u = Rr(-f.HalfA + 1.4f, f.HalfA - 1.4f), v = Rr(-f.HalfD + 1.4f, f.HalfD - 1.4f);
                if (Put(f, debrisKinds[rnd.Next(debrisKinds.Length)], u, v, Rr(0, 360), 0.6f))
                {
                    break;
                }
            }
        }
        // A fallen pillar now and then.
        if (_mat.Column[0].Prefab == "stone_pillar" && f.Area > 60 && rnd.NextDouble() < _decay * 0.6)
        {
            for (int t = 0; t < 30; t++)
            {
                float pu = Rr(-f.HalfA + 2.1f, f.HalfA - 2.1f), pv = Rr(-f.HalfD + 2.1f, f.HalfD - 2.1f);
                if (f.Free(pu, pv, 1.3f))
                {
                    FallenPillar(f, "stone_pillar", pu, pv, Rr(0, 360), 1.3f);
                    break;
                }
            }
        }
    }
}

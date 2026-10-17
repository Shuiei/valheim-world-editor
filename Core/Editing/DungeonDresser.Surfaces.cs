
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- What hangs on walls.
    /// <summary>Up to count hangings on the walls, from choice or else the biome's.</summary>
    private void Hangings(Frame f, int count, params string[] choice)
    {
        string[] options = choice.Length > 0 ? choice : _b.Hanging;
        var spots = f.WallSpots(3.2f, 3f).OrderBy(_ => _rnd.Next()).Take(count).ToList();
        string last = "";
        foreach (var s in spots)
        {
            last = Another(options, last);
            Hang(f, last, s);
        }
    }

    /// <summary>Hangs a prefab at a wall spot as <see cref="DungeonProps.Hanging"/> says; nothing when it does not fit.</summary>
    private void Hang(Frame f, string prefab, (float U, float V, float Yaw) s)
    {
        var h = DungeonProps.Hanging(prefab, f.H);
        if (h == null)
        {
            return;
        }
        var (u, v) = In(s, h.Value.Inward);
        _o.Add(prefab, f.W(u, v, f.Top + h.Value.Y), f.Yaw(s.Yaw + h.Value.Turn));
        f.Take(u, v, 0.3f);
    }

    /// <summary>A runner rug down the middle, from v0 to v1 (one small rug when there is no room for two pieces).</summary>
    private void Runner(Frame f, float v0, float v1, float u = 0)
    {
        const float Len = 4.36f;
        int n = (int)MathF.Floor((v1 - v0) / Len);
        if (n < 2)
        {
            Put(f, One("rug_wolf", "rug_fur", "rug_deer"), u, (v0 + v1) / 2, 0, 0, force: true);
            return;
        }
        float start = (v0 + v1) / 2 - n * Len / 2 + Len / 2;
        for (int i = 0; i < n; i++)
        {
            string piece = i == 0 ? "Morkhalla_Rug_end1" : i == n - 1 ? "Morkhalla_Rug_end2" : "Morkhalla_Rug_middle";
            _o.Add(piece, f.W(u, start + i * Len, f.Top + 0.06f), f.Yaw(i == n - 1 ? 180 : 0));
        }
    }

    // ---- What lies about.
    /// <summary>
    /// Things lying about, perArea a square metre (more with Decor), mostly by the walls; from choice or
    /// else the biome's clutter.
    /// </summary>
    private void Clutter(Frame f, float perArea, params string[] choice)
    {
        string[] options = choice.Length > 0 ? choice : _b.Clutter;
        // Some rooms are bare, some cluttered (a density of the room's own), and nothing stands twice running.
        int n = Count(f.Area * perArea * (0.5f + 0.5f * _s.Decor) * R(0.5f, 1.5f));
        var walls = f.WallSpots(1.3f, 2.5f).OrderBy(_ => _rnd.Next()).ToList();
        string last = "";
        for (int i = 0; i < n; i++)
        {
            string thing = Another(options, last);
            if (i < walls.Count && Chance(0.7))
            {
                var (u, v) = In(walls[i], R(0.5f, 1.1f));
                Put(f, thing, u, v, R(0, 360), 0.45f);
            }
            else if (Spot(f, 0.5f) is var (u, v))
            {
                Put(f, thing, u, v, R(0, 360), 0.45f);
            }
            last = thing;
        }
    }

    /// <summary>Asks for the room's decay (rubble, broken walls and pots, a fallen pillar): placed once the room is furnished.</summary>
    private void Decay() => _decayed = true;

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

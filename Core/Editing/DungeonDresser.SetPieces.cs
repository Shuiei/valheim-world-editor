
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- Set pieces.
    /// <summary>
    /// A raised stone platform 1 m high, w × d metres (even) centred at (u, v), of stone blocks on the marble (through
    /// any wooden floor: stone on wood would not hold); the floor round it kept.
    /// </summary>
    private void Plinth(Frame f, float u, float v, float w, float d)
    {
        for (float du = -w / 2 + 1; du < w / 2; du += 2)
        {
            for (float dv = -d / 2 + 1; dv < d / 2; dv += 2)
            {
                if (f.Inside(u + du, v + dv))
                {
                    _o.Add("stone_floor_2x2", f.W(u + du, v + dv, f.F + 0.5f + Lift), f.Yaw(0));
                }
            }
        }
        f.Take(u, v, MathF.Sqrt(w * w + d * d) / 2 + 0.3f);
    }

    /// <summary>
    /// Steps up a platform's side: a row of stone stairs w metres wide at (u, v), turned to yaw. A stone stair climbs
    /// toward its back, so they rise away from yaw: steps before a platform at +v are turned 180.
    /// </summary>
    private void Steps(Frame f, float u, float v, float yaw, float w)
    {
        for (float du = -w / 2 + 1; du < w / 2; du += 2)
        {
            var (su, sv) = (u + du * MathF.Cos(yaw * MathF.PI / 180), v - du * MathF.Sin(yaw * MathF.PI / 180));
            if (f.Inside(su, sv))
            {
                _o.Add("stone_stair", f.W(su, sv, f.F + Lift), f.Yaw(yaw));
            }
        }
    }

    /// <summary>Pilasters: columns half sunk into the walls every step metres, not by doorways.</summary>
    private void Pilasters(Frame f, float step)
    {
        foreach (var sp in f.WallSpots(step, 2.6f))
        {
            var (u, v) = In(sp, 0.45f);
            Column(f, u, v);
        }
    }

    /// <summary>Cobwebs up in some of the corners, more in a decayed dungeon (from a stream of the room's own: the decay setting moves nothing else).</summary>
    private void Webs(Frame f, float chance)
    {
        var rnd = Local(f, 3);
        string[] webs = ["morkhalla_web_corner", "vertical_web"];
        foreach (var (u, v, yaw, convex) in f.Corners())
        {
            if (convex && rnd.NextDouble() < chance)
            {
                var (cu, cv) = In((u, v, yaw), 0.35f);
                _o.Add(webs[rnd.Next(webs.Length)], f.W(cu, cv, f.Top + f.H - 1.2f - (float)rnd.NextDouble()), f.Yaw(yaw + 180));
            }
        }
    }

    /// <summary>Hooks on chains hanging from the roof of a tall room, here and there (a low room gets none: nothing to hang).</summary>
    private void Chains(Frame f, int n)
    {
        if (f.H < 8)
        {
            return;
        }
        for (int i = 0; i < n; i++)
        {
            if (Spot(f, 0.3f, 1.5f) is var (u, v))
            {
                _o.Add("dvergrprops_hooknchain", f.W(u, v, f.Top + f.H - 2.5f), f.Yaw(R(0, 360)));
            }
        }
    }

    /// <summary>Torches on the walls (sconces) every step metres, at head height; none by doorways.</summary>
    private void WallTorches(Frame f, float step)
    {
        foreach (var sp in f.WallSpots(step, 2.5f))
        {
            var (u, v) = In(sp, 0.25f);
            _o.Add("piece_walltorch", f.W(u, v, f.Top + 2.1f), f.Yaw(sp.Yaw));
        }
    }
}

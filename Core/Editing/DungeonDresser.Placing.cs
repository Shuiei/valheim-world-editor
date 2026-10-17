
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- Placing.

    /// <summary>
    /// Places an object at (u, v) of a room, y above its floor (its foot, from the model), turned yaw
    /// (local; 0 faces the far end), keeping r metres around it for itself unless forced.
    /// </summary>
    /// <returns>False when the spot is not free (and it is not forced): nothing is placed.</returns>
    private bool Put(Frame f, string prefab, float u, float v, float yaw, float r, float y = 0, bool force = false,
        IReadOnlyList<(string, string, string)>? data = null, bool take = true)
    {
        if (!force && !f.Free(u, v, r) || force && !f.Inside(u, v))
        {
            return false;
        }
        if (r > 0 && take)
        {
            f.Take(u, v, r);
        }
        _o.Add(prefab, f.W(u, v, (OnMarble(prefab) ? f.F : f.Top) + Lift + y + DungeonProps.Foot(prefab)), f.Yaw(yaw + DungeonProps.Turn(prefab)), data);
        return true;
    }

    /// <summary>
    /// Whether a piece must stand on the marble itself, through any floor finish: a piece of stone, marble or grausten
    /// needs more support than a wooden floor or a marble tile gives (100 at the least; a floor passes on less).
    /// </summary>
    private static bool OnMarble(string prefab) => Stability.Known(prefab) && Stability.Shape(prefab).Material is 1 or 4 or 5;

    /// <summary>Tries a few spots near (u, v), up to spread metres off; false when none is free.</summary>
    private bool PutNear(Frame f, string prefab, float u, float v, float yaw, float r, float spread = 1.5f, float y = 0)
    {
        for (int i = 0; i < 8; i++)
        {
            if (Put(f, prefab, u + (i == 0 ? 0 : R(-spread, spread)), v + (i == 0 ? 0 : R(-spread, spread)), yaw, r, y))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Somewhere free in the room for a circle of radius r, margin metres from the walls; null after 30 tries. The
    /// tries are drawn from rnd when given (a stream of the room's own), else from the dungeon's.
    /// </summary>
    private (float U, float V)? Spot(Frame f, float r, float margin = 0.8f, Random? rnd = null)
    {
        rnd ??= _rnd;
        for (int i = 0; i < 30; i++)
        {
            float u = -f.HalfA + margin + r + (float)rnd.NextDouble() * 2 * (f.HalfA - margin - r);
            float v = -f.HalfD + margin + r + (float)rnd.NextDouble() * 2 * (f.HalfD - margin - r);
            if (f.Free(u, v, r))
            {
                return (u, v);
            }
        }
        return null;
    }

    /// <summary>
    /// A column of the material's segments from floor to roof at (u, v), keeping the floor round it: the last may rise
    /// into the roof slab, never past it into the room above.
    /// </summary>
    /// <summary>Up to three shelves against the walls, each spot at least clear metres from the corners.</summary>
    private void Shelves(Frame f, float clear)
    {
        foreach (var s in f.WallSpots(clear, 2.8f).OrderBy(_ => _rnd.Next()).Take(3))
        {
            var (u, v) = In(s, 0.12f);
            Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.6f);
        }
    }

    private void Column(Frame f, float u, float v)
    {
        if (!f.Inside(u, v))
        {
            return;
        }
        var p = f.W(u, v, 0);
        DungeonShell.Stack(_mat.Column, _o, p.X, p.Z, f.F, f.F + f.H);
        f.Take(u, v, 0.8f);
    }

    /// <summary>A wall of the material inside a room: across (u0..u1 at depth v) or along (v0..v1 at u), y0..y1 above the floor.</summary>
    private void InnerWall(Frame f, float u0, float v0, float u1, float v1, float y0, float y1)
    {
        var a = f.W(u0, v0, 0);
        var b = f.W(u1, v1, 0);
        bool alongZ = MathF.Abs(a.X - b.X) < 0.01f;
        float line = alongZ ? a.X : a.Z, t0 = alongZ ? Math.Min(a.Z, b.Z) : Math.Min(a.X, b.X), t1 = alongZ ? Math.Max(a.Z, b.Z) : Math.Max(a.X, b.X);
        DungeonShell.Fill(_mat, _o, alongZ, line, t0, t1, f.F + y0, f.F + y1);
    }
}

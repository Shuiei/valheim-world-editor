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
}

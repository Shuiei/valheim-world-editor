namespace TerrainEditor.Desktop;

/// <summary>The cut ground of the picking: whether a mod's extension cuts the ground away under a point.</summary>
public static partial class Picking
{
    /// <summary>
    /// Whether the ground is cut away (left undrawn by a mod's extension) under a view-space point (x, z): the cut
    /// values blended over the terrain's triangle there (each grid cell is two, split from its east to its south
    /// point, as BuildTerrain makes them), past a half, as the terrain shader leaves them out.
    /// </summary>
    public static bool CutAt(WorldScene s, float x, float z)
    {
        if (s.Cut.Length != s.W * s.H)
        {
            return false;
        }
        if (CellAt(s, x, z) is not var (x0, z0, tx, tz))
        {
            return false;
        }
        float C(int a, int b) => s.Cut[b * s.W + a];
        float value = tx + tz <= 1
            ? C(x0, z0) * (1 - tx - tz) + C(x0 + 1, z0) * tx + C(x0, z0 + 1) * tz
            : C(x0 + 1, z0 + 1) * (tx + tz - 1) + C(x0 + 1, z0) * (1 - tz) + C(x0, z0 + 1) * (1 - tx);
        return value > 0.5f;
    }

    /// <summary>
    /// The grid cell under a view-space point: its corner (X0, Z0) and the point's place inside it (Tx, Tz from
    /// 0 to 1), or null when the point is outside the area's ground.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="x">The view-space x.</param>
    /// <param name="z">The view-space z.</param>
    private static (int X0, int Z0, float Tx, float Tz)? CellAt(WorldScene s, float x, float z)
    {
        float gx = x + (s.W - 1) / 2f, gz = -z + (s.H - 1) / 2f;
        if (gx < 0 || gz < 0 || gx > s.W - 1 || gz > s.H - 1)
        {
            return null;
        }
        int x0 = Math.Min((int)gx, s.W - 2), z0 = Math.Min((int)gz, s.H - 2);
        return (x0, z0, gx - x0, gz - z0);
    }
}

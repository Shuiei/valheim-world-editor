namespace TerrainEditor.Desktop;

/// <summary>
/// The ground's height over a rectangle or a point of the open area, for building on it.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>The highest ground under a rectangle (every metre); edgesOnly: only within a metre of its edges.</summary>
    private static float HighestGround(WorldScene scene, float x0, float z0, float x1, float z1, bool edgesOnly = false)
    {
        (float lx, float hx, float lz, float hz) = (MathF.Min(x0, x1), MathF.Max(x0, x1), MathF.Min(z0, z1), MathF.Max(z0, z1));
        // Whole-metre steps counted in integers: adding 1 to a float that large would not move it on.
        int nx = (int)MathF.Floor(hx - lx), nz = (int)MathF.Floor(hz - lz);
        float best = float.MinValue;
        for (int i = 0; i <= nx; i++)
        {
            float x = lx + i;
            for (int j = 0; j <= nz; j++)
            {
                float z = lz + j;
                bool edge = i < 1 || hx - x < 1 || j < 1 || hz - z < 1;
                if (!edgesOnly || edge)
                {
                    best = MathF.Max(best, GroundAt(scene, x, z));
                }
            }
        }
        return best;
    }

    /// <summary>
    /// The ground's height at a world point of the area, between the grid points as ground_at gives it (Picking.HeightAt),
    /// the point held to the area's edge (a coordinate that is not a number: its west or south edge).
    /// </summary>
    private static float GroundAt(WorldScene scene, float x, float z)
    {
        float hx = (scene.W - 1) / 2f, hz = (scene.H - 1) / 2f, vx = x - scene.Cx, vz = z - scene.Cz;
        vx = vx > -hx ? (vx < hx ? vx : hx) : -hx;
        vz = vz > -hz ? (vz < hz ? vz : hz) : -hz;
        return Picking.HeightAt(scene, vx, -vz);
    }
}

namespace TerrainEditor.Desktop;

/// <summary>The area test and the ground height of the picking, under a view-space point.</summary>
public static partial class Picking
{
    /// <summary>Whether a view-space point (x, z) is over the area's ground.</summary>
    public static bool Inside(WorldScene s, float x, float z)
    {
        float gx = x + (s.W - 1) / 2f, gz = -z + (s.H - 1) / 2f;
        return gx >= 0 && gz >= 0 && gx <= s.W - 1 && gz <= s.H - 1;
    }

    /// <summary>
    /// The ground's height under a view-space point (between the grid points), or -1000 outside the area.
    /// </summary>
    public static float HeightAt(WorldScene s, float x, float z)
    {
        if (CellAt(s, x, z) is not var (x0, z0, tx, tz))
        {
            return -1000;
        }
        float H(int a, int b) => s.Heights[b * s.W + a];
        return (H(x0, z0) * (1 - tx) + H(x0 + 1, z0) * tx) * (1 - tz) + (H(x0, z0 + 1) * (1 - tx) + H(x0 + 1, z0 + 1) * tx) * tz;
    }
}

namespace TerrainEditor.Desktop;

// The grid points a stroke or a stamp may change: the rectangle's points the ground does not lock.
public static partial class Sculpt
{
    /// <summary>The grid points of a rectangle the ground lets change (the locked ones left out), row by row.</summary>
    internal static IEnumerable<(int Gx, int Gz)> Unlocked(Ground g, int x0, int z0, int x1, int z1)
    {
        if (x0 > x1 || z0 > z1)
        {
            yield break;
        }
        int width = x1 - x0 + 1;
        for (int i = 0; i < width * (z1 - z0 + 1); i++)
        {
            int gx = x0 + i % width, gz = z0 + i / width;
            if (!g.Locked(gx, gz))
            {
                yield return (gx, gz);
            }
        }
    }
}

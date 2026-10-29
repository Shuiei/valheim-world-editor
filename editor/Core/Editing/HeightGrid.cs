namespace TerrainEditor.Editing;

/// <summary>
/// The ground of a block of zones as one grid of heights (65 points per zone side, shared edges once):
/// the generated ground plus the edits, within the game's ±8 m (plus the No limit lift not saved yet).
/// </summary>
public static class HeightGrid
{
    /// <summary>
    /// Reads the heights of zones <paramref name="x0"/>..<paramref name="x1"/> by <paramref name="z0"/>..<paramref name="z1"/>
    /// (zone indices, both ends included).
    /// </summary>
    /// <param name="terrain">Gives each zone's generated ground.</param>
    /// <param name="edits">The edits laid over it; zones without one keep the generated ground.</param>
    /// <param name="x0">First zone column.</param>
    /// <param name="z0">First zone row.</param>
    /// <param name="x1">Last zone column, included.</param>
    /// <param name="z1">Last zone row, included.</param>
    /// <returns>
    /// The grid's width and height in points and its heights (metres), one row of x per z, from the
    /// (<paramref name="x0"/>, <paramref name="z0"/>) corner.
    /// </returns>
    public static (int W, int H, float[] Heights) Read(ValheimGen.TerrainService terrain, EditStore edits, int x0, int z0, int x1, int z1)
    {
        int w = (x1 - x0 + 1) * 64 + 1, h = (z1 - z0 + 1) * 64 + 1;
        float[] heights = new float[w * h];
        for (int zz = z0; zz <= z1; zz++)
        {
            for (int zx = x0; zx <= x1; zx++)
            {
                float[] b = terrain.BaseZone(zx, zz);
                ZoneEdit? e = edits.Get(zx, zz);
                int ox = (zx - x0) * 64, oz = (zz - z0) * 64;
                for (int k = 0; k < EditStore.Grid; k++)
                {
                    for (int l = 0; l < EditStore.Grid; l++)
                    {
                        int i = k * EditStore.Grid + l;
                        heights[(oz + k) * w + ox + l] = e?.HeightAt(i, b[i]) ?? b[i];
                    }
                }
            }
        }
        return (w, h, heights);
    }
}

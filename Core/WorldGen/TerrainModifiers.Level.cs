using System.Numerics;

namespace TerrainEditor.Terrain;

public sealed partial class TerrainModifiers
{
    /// <summary>
    /// Heightmap.LevelTerrain (WorldToVertexMask, square or round area, sets the modifier's height).
    /// </summary>
    internal static void LevelTerrain(int zx, int zz, float[] h, Vector3 worldPos, float radius, bool square, float[]? baseHeights, float[]? levelOnly, bool player)
    {
        float dx = worldPos.X - zx * 64f, dz = worldPos.Z - zz * 64f;
        int half = (Width + 1) / 2;
        int x = (int)MathF.Floor(dx / 1f + 0.5f + half);
        int y = (int)MathF.Floor(dz / 1f + 0.5f + half);
        float num = (float)((double)radius / 1.0);
        int n = Width + 1;
        ForEachVertexInReach(x, y, (int)MathF.Ceiling(num), (i, j) =>
        {
            if ((square || !(Distance(x, y, j, i) > num)) && j >= 0 && i >= 0 && j < n && i < n)
            {
                float v = worldPos.Y;
                if (player)
                {
                    float b = baseHeights![i * n + j];
                    v = levelOnly![i * n + j] = Math.Clamp(v, (float)((double)b - 8.0), (float)((double)b + 8.0));
                }
                h[i * n + j] = v;
            }
        });
    }

    /// <summary>
    /// Calls <paramref name="visit"/> with the row and column of each vertex in the square of reach <paramref name="reach"/>
    /// around (<paramref name="x"/>, <paramref name="y"/>), row by row; vertices off the zone are visited too (the callers check).
    /// </summary>
    internal static void ForEachVertexInReach(int x, int y, int reach, Action<int, int> visit)
    {
        for (int i = y - reach; i <= y + reach; i++)
        {
            for (int j = x - reach; j <= x + reach; j++)
            {
                visit(i, j);
            }
        }
    }
}

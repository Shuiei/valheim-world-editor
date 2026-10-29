using System.Numerics;

namespace TerrainEditor.Terrain;

public sealed partial class TerrainModifiers
{
    /// <summary>
    /// Heightmap.SmoothTerrain2 (WorldToVertex, round area, blend towards the modifier's height).
    /// </summary>
    internal static void SmoothTerrain2(int zx, int zz, float[] h, Vector3 worldPos, float radius, float[]? levelOnly, float power, bool player)
    {
        // No reach, nothing to smooth (the middle point's share would be 0 / 0: a NaN height).
        if (radius <= 0f)
        {
            return;
        }
        float dx = worldPos.X - zx * 64f, dz = worldPos.Z - zz * 64f;
        int x = (int)MathF.Floor(dx / 1f + 0.5f) + Width / 2;
        int y = (int)MathF.Floor(dz / 1f + 0.5f) + Width / 2;
        float b = worldPos.Y;
        float num = radius / 1f;
        int n = Width + 1;
        ForEachVertexInReach(x, y, (int)MathF.Ceiling(num), (i, j) =>
        {
            float d = Distance(x, y, j, i);
            if (d > num)
            {
                return;
            }
            float num5 = d / num;
            if (j >= 0 && i >= 0 && j < n && i < n)
            {
                num5 = power != 3f ? MathF.Pow(num5, power) : (float)((double)num5 * num5 * num5);
                float t = (float)(1.0 - num5);
                float v = ValheimGen.GenMath.Lerp(h[i * n + j], b, t);
                if (player)
                {
                    float lo = levelOnly![i * n + j];
                    v = Math.Clamp(v, (float)((double)lo - 1.0), (float)((double)lo + 1.0));
                }
                h[i * n + j] = v;
            }
        });
    }
}

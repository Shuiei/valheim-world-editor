
namespace TerrainEditor.Terrain;

public sealed partial class TerrainModifiers
{
    /// <summary>
    /// A modifier's cleared paint, as the game was seen to paint it (a probe of Heightmap's paint mask on a dedicated
    /// server, every paint type over several biomes): each vertex within the paint radius of the vertex nearest the
    /// modifier blends towards the paint by (1 - distance / radius)^0.1 times the strength, after the modifier's
    /// levelling and smoothing. ClearVegetation blends all four channels towards 0; the others blend red, green and
    /// blue and keep the vegetation (alpha). With the height check, vertices above the modifier are left.
    /// </summary>
    internal static void PaintCleared(int zx, int zz, float[] h, float[] mask, Modifier m)
    {
        float radius = m.PaintRadius;
        if (radius <= 0f)
        {
            return;
        }
        (float r, float g, float b, float a) = m.PaintType switch
        {
            Paint.Dirt => (1f, 0f, 0f, 1f),
            Paint.Cultivate => (0f, 1f, 0f, 1f),
            Paint.Paved => (0f, 0f, 1f, 1f),
            Paint.Reset => (0f, 0f, 0f, 1f),
            Paint.ClearVegetation => (0f, 0f, 0f, 0f),
            Paint.DeepSnow => (1f, 1f, 1f, 1f),
            _ => throw new InvalidDataException($"Unknown terrain paint type {(int)m.PaintType}"),
        };
        bool alpha = m.PaintType == Paint.ClearVegetation;
        int x = (int)MathF.Floor(m.Position.X - zx * 64f + 0.5f) + Width / 2;
        int y = (int)MathF.Floor(m.Position.Z - zz * 64f + 0.5f) + Width / 2;
        int reach = (int)MathF.Ceiling(radius);
        int n = Width + 1;
        for (int i = Math.Max(0, y - reach); i <= Math.Min(n - 1, y + reach); i++)
        {
            for (int j = Math.Max(0, x - reach); j <= Math.Min(n - 1, x + reach); j++)
            {
                float d = Distance(x, y, j, i);
                if (d > radius || (m.PaintHeightCheck && h[i * n + j] > m.Position.Y))
                {
                    continue;
                }
                float t = Math.Clamp(MathF.Pow(1f - d / radius, 0.1f) * m.PaintStrength, 0f, 1f);
                int o = (i * n + j) * 4;
                mask[o] += (r - mask[o]) * t;
                mask[o + 1] += (g - mask[o + 1]) * t;
                mask[o + 2] += (b - mask[o + 2]) * t;
                if (alpha)
                {
                    mask[o + 3] += (a - mask[o + 3]) * t;
                }
            }
        }
    }
}

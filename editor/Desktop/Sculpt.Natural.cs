
namespace TerrainEditor.Desktop;

// Naturalize: the height each point is pulled towards, worked out once per stroke.
public static partial class Sculpt
{
    /// <summary>
    /// Naturalize's aim: the ground's broad shape (blurred, from the start of the stroke) plus detail noise, in world
    /// coordinates so the pattern is continuous across areas.
    /// </summary>
    private static float NaturalTarget(Ground g, Brush b, Stroke s, int p)
    {
        if (s.Natural.TryGetValue(p, out float t))
        {
            return t;
        }
        int gx = p % g.W, gz = p / g.W, r = Math.Max(2, Math.Min(8, (int)MathF.Round(b.NoiseSize / 3)));
        float sum = 0;
        int n = 0;
        for (int dz = -r; dz <= r; dz += 2)
        {
            for (int dx = -r; dx <= r; dx += 2)
            {
                int x = Math.Clamp(gx + dx, 0, g.W - 1), z = Math.Clamp(gz + dz, 0, g.H - 1);
                sum += s.StartHeight(g, z * g.W + x);
                n++;
            }
        }
        float wx = g.OriginX + gx, wz = g.OriginZ + gz;
        t = sum / n + b.NoiseAmp * b.Noise.Fbm(wx / b.NoiseSize, wz / b.NoiseSize);
        s.Natural[p] = t;
        return t;
    }
}

namespace ValheimGen;

/// <summary>
/// Base (unmodified) terrain heights for one zone, exactly as HeightmapBuilder.Build computes them.
/// </summary>
public static class BaseTerrain
{
    /// <summary>
    /// Metres per zone side; a zone has Width + 1 vertices per side.
    /// </summary>
    public const int Width = 64;

    /// <summary>
    /// The 65x65 vertex heights of zone (zx, zz), row by row (row is z), starting at the zone's corner (its
    /// centre less 32 m on each axis).
    /// </summary>
    public static float[] BuildZone(WorldGenerator gen, int zx, int zz) => BuildZone(gen, zx, zz, null);

    /// <summary>The corners' blend weight at each vertex along a side: the same for rows and columns.</summary>
    private static readonly float[] Blend = [.. Enumerable.Range(0, Width + 1).Select(i => GenMath.Smooth(0f, 1f, (float)((double)i / Width)))];

    /// <summary>
    /// The same, also filling a base paint mask.
    /// </summary>
    /// <param name="gen">The world's generator.</param>
    /// <param name="zx">Zone x.</param>
    /// <param name="zz">Zone z.</param>
    /// <param name="mask">Optional 65x65 RGBA base paint mask (Heightmap m_baseMask), as GetBiomeHeight reports it,
    /// filled in place (4 floats per vertex).</param>
    public static float[] BuildZone(WorldGenerator gen, int zx, int zz, float[]? mask)
    {
        const float Scale = 1f;
        int num = Width + 1;
        float[] heights = new float[num * num];
        float vx = zx * 64f + Width * Scale * -0.5f;
        float vz = zz * 64f + Width * Scale * -0.5f;
        float far = (float)((double)Width * Scale);
        Heightmap.Biome b1 = gen.GetBiome(vx, vz);
        Heightmap.Biome b2 = gen.GetBiome((float)((double)vx + far), vz);
        Heightmap.Biome b3 = gen.GetBiome(vx, (float)((double)vz + far));
        Heightmap.Biome b4 = gen.GetBiome((float)((double)vx + far), (float)((double)vz + far));
        for (int k = 0; k < num; k++)
        {
            float wy = (float)((double)vz + k * (double)Scale);
            float t = Blend[k];
            for (int l = 0; l < num; l++)
            {
                float wx = (float)((double)vx + l * (double)Scale);
                float t2 = Blend[l];
                float h;
                Color m = default;
                if (b3 == b1 && b2 == b1 && b4 == b1)
                {
                    h = gen.GetBiomeHeight(b1, wx, wy, out m);
                }
                else
                {
                    float h1 = gen.GetBiomeHeight(b1, wx, wy, out Color m1);
                    float h2 = gen.GetBiomeHeight(b2, wx, wy, out Color m2);
                    float h3 = gen.GetBiomeHeight(b3, wx, wy, out Color m3);
                    float h4 = gen.GetBiomeHeight(b4, wx, wy, out Color m4);
                    h = GenMath.Lerp(GenMath.Lerp(h1, h2, t2), GenMath.Lerp(h3, h4, t2), t);
                    if (mask != null)
                    {
                        m = Lerp(Lerp(m1, m2, t2), Lerp(m3, m4, t2), t);
                    }
                }
                heights[k * num + l] = h;
                if (mask != null)
                {
                    int o = (k * num + l) * 4;
                    mask[o] = m.r; mask[o + 1] = m.g; mask[o + 2] = m.b; mask[o + 3] = m.a;
                }
            }
        }
        return heights;
    }

    /// <summary>
    /// a to b by t, held to 0..1, per channel.
    /// </summary>
    private static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }
}

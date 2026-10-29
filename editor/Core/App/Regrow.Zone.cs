using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// One zone's heightmap as the game sees it while placing vegetation: biomes, the paint mask and the ocean depth.
/// </summary>
public static partial class Regrow
{
    /// <summary>
    /// One zone's heightmap as the game sees it while placing: corner biomes, edge or middle of a biome,
    /// the vegetation mask and the ocean depth at its corners.
    /// </summary>
    private sealed class Zone
    {
        /// <summary>The biome at each corner, as <c>TerrainService.CornerBiomes</c> gives them.</summary>
        public required int[] Corners;
        /// <summary>The base paint mask: 65 × 65 vertices, four floats (RGBA) each.</summary>
        public required float[] Mask;
        /// <summary>Ocean depth (metres below water) at the heightmap's four corners, in the game's corner order.</summary>
        public required float[] OceanDepth;
        /// <summary>The zone's middle in world metres.</summary>
        public float Cx, Cz;

        /// <summary>Whether any corner has one of these biomes (bit flags).</summary>
        public bool HaveBiome(int biome) => Corners.Any(c => (c & biome) != 0);

        /// <summary>The game's biome area: 2 (median) when all four corners are one biome, else 1 (edge).</summary>
        public int Area => Corners[0] == Corners[1] && Corners[0] == Corners[2] && Corners[0] == Corners[3] ? 2 : 1;   // Median : Edge

        /// <summary>The biome at a world point, weighting the corners by distance as the game does.</summary>
        public int Biome(float x, float z)
        {
            if (Corners[0] == Corners[1] && Corners[0] == Corners[2] && Corners[0] == Corners[3])
            {
                return Corners[0];
            }
            float nx = (x - Cx) / 64f + 0.5f, ny = (z - Cz) / 64f + 0.5f;
            // Each corner's weight added to its biome's, corners in the game's order (0,0), (1,0), (0,1), (1,1).
            Span<int> biomes = stackalloc int[4];
            Span<float> weights = stackalloc float[4];
            int count = 0;
            for (int c = 0; c < 4; c++)
            {
                float dx = nx - (c & 1), dy = ny - (c >> 1), d = MathF.Sqrt(dx * dx + dy * dy), k = 1.414f - d;
                int at = biomes[..count].IndexOf(Corners[c]);
                if (at < 0)
                {
                    at = count++;
                    biomes[at] = Corners[c];
                    weights[at] = 0f;
                }
                weights[at] += k * k * k;
            }
            // Ties go to the lowest biome index, as the game walks its weights in index order.
            int best = 0;
            for (int i = 1; i < count; i++)
            {
                if (weights[i] > weights[best] || (weights[i] == weights[best] && BitOperations.Log2((uint)biomes[i]) < BitOperations.Log2((uint)biomes[best])))
                {
                    best = i;
                }
            }
            return biomes[best];
        }

        /// <summary>Heightmap.GetVegetationMask: the alpha of the base paint mask at the vertex under the point.</summary>
        public float VegetationMask(float x, float z)
        {
            int mx = Math.Clamp((int)MathF.Floor(x - 0.5f - Cx + 0.5f + 32f), 0, 64), my = Math.Clamp((int)MathF.Floor(z - 0.5f - Cz + 0.5f + 32f), 0, 64);
            return Mask[(my * 65 + mx) * 4 + 3];
        }

        /// <summary>The ocean depth at a world point, between the four corners' depths.</summary>
        public float Ocean(float x, float z)
        {
            int vx = (int)MathF.Floor(x - Cx + 0.5f) + 32, vy = (int)MathF.Floor(z - Cz + 0.5f) + 32;
            float t = vx / 64f, t2 = vy / 64f;
            float a = Lerp(OceanDepth[3], OceanDepth[2], t), b = Lerp(OceanDepth[0], OceanDepth[1], t);
            return Lerp(a, b, t2);
        }
    }

    /// <summary>Unity's Mathf.Lerp: t clamped to 0..1.</summary>
    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}

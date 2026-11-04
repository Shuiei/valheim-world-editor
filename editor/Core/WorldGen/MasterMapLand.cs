namespace ValheimGen;

/// <summary>
/// The land a master map draws, as the world generator asks it in place of the game's continents (GetBaseHeight) and
/// biome layout (GetBiome): everything else (each biome's own shape, the rivers and lakes the game lays out on that
/// land, the forests, the vegetation and the locations) stays the game's. Made once per map: distance fields over the
/// cells give each kind of ground its height (coasts rising from the sea, foothills rising to the mountains, mountains
/// higher the further in, the sea deeper the further out), then a light blur; asked between the cells' middles.
/// All in plain IEEE arithmetic (no transcendental functions), so that the game's mod works out the same values.
/// </summary>
internal sealed class MasterMapLand : IDrawnLand
{
    /// <summary>The continents' height at the coast (just under the game's waterline, 0.15).</summary>
    private const float Coast = 0.13f;

    /// <summary>The continents' height where the mountains begin (the game's Mountain threshold).</summary>
    private const float MountainFoot = 0.4f;

    /// <summary>Cells on a side.</summary>
    private readonly int _n;

    /// <summary>Metres a cell, and the square's half.</summary>
    private readonly float _cell, _half;

    /// <summary>The continents' height at each cell's middle.</summary>
    private readonly float[] _land;

    /// <summary>The game's biome of each cell (a river's or a lake's: its banks').</summary>
    private readonly Heightmap.Biome[] _biome;

    /// <summary>The land of these cells.</summary>
    /// <param name="cells">The master map's kinds (TerrainEditor.App.MasterMap.Kind), row by row from the north.</param>
    /// <param name="n">Cells on a side.</param>
    /// <param name="half">Half the square they cover, metres.</param>
    /// <param name="biomes">The game's biome of each kind (None for river and lake).</param>
    internal MasterMapLand(byte[] cells, int n, float half, Heightmap.Biome[] biomes)
    {
        _n = n;
        _half = half;
        _cell = 2 * half / n;
        const byte River = 8, Lake = 9, Sea = 10, DeepSea = 11, Mountain = 3;
        static bool Water(byte k) => k >= River;
        int count = n * n;
        // Distances, in cells: from land to the nearest water, from water to the nearest land (and which), from the
        // foot of the mountains and from inside them to their edge, and from the deep sea to shallower water.
        float[] toWater = Distances(cells, n, Water, out _);
        float[] toLand = Distances(cells, n, k => !Water(k), out int[] nearestLand);
        float[] toMountain = Distances(cells, n, k => k == Mountain, out _);
        float[] toLowland = Distances(cells, n, k => k != Mountain, out _);
        float[] toShallow = Distances(cells, n, k => k != DeepSea, out _);
        float[] raw = new float[count];
        _biome = new Heightmap.Biome[count];
        for (int i = 0; i < count; i++)
        {
            byte k = cells[i];
            float v;
            if (k == Mountain)
            {
                v = MountainFoot + 0.35f * Ease(toLowland[i] * _cell / 2000f);
            }
            else if (!Water(k))
            {
                v = Coast + (Inland(k) - Coast) * Ease(toWater[i] * _cell / 500f);
                float foothills = 1f - Ease(toMountain[i] * _cell / 800f);
                if (v < MountainFoot)
                {
                    v += (MountainFoot - v) * foothills * foothills;
                }
            }
            else if (k == Sea)
            {
                v = Coast + (-0.02f - Coast) * Ease(toLand[i] * _cell / 500f);
            }
            else if (k == DeepSea)
            {
                float shallow = Coast + (-0.02f - Coast) * Ease(toLand[i] * _cell / 500f);
                v = shallow + (-0.25f - shallow) * Ease(toShallow[i] * _cell / 1500f);
            }
            else if (k == Lake)
            {
                v = 0.12f + (0f - 0.12f) * Ease(toLand[i] * _cell / 200f);
            }
            else
            {
                v = 0.03f;
            }
            raw[i] = v;
            _biome[i] = Water(k) && biomes[k] == Heightmap.Biome.None ? biomes[cells[nearestLand[i]]] : biomes[k];
        }
        // Softened (two passes of a 5-cell box, about 50 m), the rivers and lakes kept as deep as drawn.
        _land = Blur(Blur(raw, n, 2), n, 2);
        for (int i = 0; i < count; i++)
        {
            if (cells[i] is River or Lake && raw[i] < _land[i])
            {
                _land[i] = raw[i];
            }
        }
    }

    /// <summary>The continents' height a land kind rises to inland, past the coast.</summary>
    private static float Inland(byte kind) => kind switch
    {
        0 => 0.24f, // Meadows
        1 => 0.30f, // Black Forest
        2 => 0.16f, // Swamp: the game keeps its ground at the waterline whatever the continents
        4 => 0.22f, // Plains: flat
        5 => 0.26f, // Mistlands: its peaks rise from there
        6 => 0.22f, // Ashlands
        7 => 0.16f, // Deep North: the game raises it by 0.1
        _ => 0.24f,
    };

    /// <summary>0 to 1, eased at both ends (smoothstep), held to 0..1.</summary>
    private static float Ease(float t)
    {
        t = t < 0f ? 0f : t > 1f ? 1f : t;
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// The continents' height at a point (the game's GetBaseHeight, before the world's edge): between the four nearest
    /// cells' middles, held to the picture's edge.
    /// </summary>
    public float Continent(float x, float z)
    {
        float fx = (x + _half) / _cell - 0.5f, fy = (_half - z) / _cell - 0.5f;
        fx = fx < 0f ? 0f : fx > _n - 1 ? _n - 1 : fx;
        fy = fy < 0f ? 0f : fy > _n - 1 ? _n - 1 : fy;
        int x0 = (int)fx, y0 = (int)fy;
        int x1 = x0 + 1 < _n ? x0 + 1 : x0, y1 = y0 + 1 < _n ? y0 + 1 : y0;
        float tx = fx - x0, ty = fy - y0;
        float top = _land[y0 * _n + x0] + (_land[y0 * _n + x1] - _land[y0 * _n + x0]) * tx;
        float bottom = _land[y1 * _n + x0] + (_land[y1 * _n + x1] - _land[y1 * _n + x0]) * tx;
        return top + (bottom - top) * ty;
    }

    /// <summary>
    /// The biome at a point: the cell's, read a little off the point (by up to 12 m, by noise) so that the edges between
    /// biomes wander instead of following the cells' steps.
    /// </summary>
    public Heightmap.Biome Biome(float x, float z)
    {
        float wx = (float)((double)x + ((double)GenMath.Noise((double)x * 0.02 + 731.0, (double)z * 0.02 + 113.0) - 0.5) * 24.0);
        float wz = (float)((double)z + ((double)GenMath.Noise((double)x * 0.02 + 257.0, (double)z * 0.02 + 919.0) - 0.5) * 24.0);
        int col = (int)Math.Floor((wx + _half) / _cell), row = (int)Math.Floor((_half - wz) / _cell);
        col = col < 0 ? 0 : col >= _n ? _n - 1 : col;
        row = row < 0 ? 0 : row >= _n ? _n - 1 : row;
        return _biome[row * _n + col];
    }

    /// <summary>
    /// The straight-line distance (in cells) from each cell to the nearest cell that <paramref name="feature"/> holds
    /// for (0 on those), and which that cell is: the exact Euclidean distance transform, by columns then by rows
    /// (Felzenszwalb and Huttenlocher's lower envelope of parabolas). Far when no cell is a feature.
    /// </summary>
    private static float[] Distances(byte[] cells, int n, Func<byte, bool> feature, out int[] nearest)
    {
        const float Far = 1e12f;
        int count = n * n;
        float[] d2 = new float[count];
        nearest = new int[count];
        bool[] isFeature = new bool[256];
        for (int k = 0; k < 256; k++)
        {
            isFeature[k] = feature((byte)k);
        }
        for (int i = 0; i < count; i++)
        {
            d2[i] = isFeature[cells[i]] ? 0f : Far;
            nearest[i] = i;
        }
        float[] f = new float[n], outD = new float[n];
        int[] fIdx = new int[n], outIdx = new int[n], v = new int[n];
        float[] zb = new float[n + 1];
        // Columns: each column's distances along it.
        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < n; y++)
            {
                f[y] = d2[y * n + x];
                fIdx[y] = nearest[y * n + x];
            }
            Envelope(f, fIdx, n, outD, outIdx, v, zb);
            for (int y = 0; y < n; y++)
            {
                d2[y * n + x] = outD[y];
                nearest[y * n + x] = outIdx[y];
            }
        }
        // Rows: across, from the columns' distances.
        for (int y = 0; y < n; y++)
        {
            Array.Copy(d2, y * n, f, 0, n);
            Array.Copy(nearest, y * n, fIdx, 0, n);
            Envelope(f, fIdx, n, outD, outIdx, v, zb);
            Array.Copy(outD, 0, d2, y * n, n);
            Array.Copy(outIdx, 0, nearest, y * n, n);
        }
        float[] d = new float[count];
        for (int i = 0; i < count; i++)
        {
            d[i] = d2[i] >= Far ? Far : MathF.Sqrt(d2[i]);
        }
        return d;
    }

    /// <summary>
    /// One line of the distance transform: out[q] = min over p of (q - p)² + f[p], and the feature that p carried.
    /// </summary>
    private static void Envelope(float[] f, int[] fIdx, int n, float[] outD, int[] outIdx, int[] v, float[] z)
    {
        const float Far = 1e12f;
        int k = -1;
        for (int q = 0; q < n; q++)
        {
            if (f[q] >= Far)
            {
                continue;
            }
            float s = 0f;
            while (k >= 0)
            {
                int p = v[k];
                s = (f[q] + (float)q * q - (f[p] + (float)p * p)) / (2f * (q - p));
                if (s > z[k])
                {
                    break;
                }
                k--;
            }
            k++;
            v[k] = q;
            z[k] = k == 0 ? float.NegativeInfinity : s;
            z[k + 1] = float.PositiveInfinity;
        }
        if (k < 0)
        {
            for (int q = 0; q < n; q++)
            {
                outD[q] = Far;
                outIdx[q] = fIdx[q];
            }
            return;
        }
        int j = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[j + 1] < q)
            {
                j++;
            }
            int p = v[j];
            outD[q] = (float)(q - p) * (q - p) + f[p];
            outIdx[q] = fIdx[p];
        }
    }

    /// <summary>A box blur of radius <paramref name="r"/> cells, across then down (held at the picture's edges).</summary>
    private static float[] Blur(float[] src, int n, int r)
    {
        float[] across = new float[src.Length], down = new float[src.Length];
        float w = 1f / (2 * r + 1);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float sum = 0f;
                for (int d = -r; d <= r; d++)
                {
                    int xx = x + d < 0 ? 0 : x + d >= n ? n - 1 : x + d;
                    sum += src[y * n + xx];
                }
                across[y * n + x] = sum * w;
            }
        }
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float sum = 0f;
                for (int d = -r; d <= r; d++)
                {
                    int yy = y + d < 0 ? 0 : y + d >= n ? n - 1 : y + d;
                    sum += across[yy * n + x];
                }
                down[y * n + x] = sum * w;
            }
        }
        return down;
    }
}

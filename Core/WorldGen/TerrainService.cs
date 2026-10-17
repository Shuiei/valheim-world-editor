using System.Collections.Concurrent;
using System.IO.Compression;
using TerrainEditor.Save;

namespace ValheimGen;

/// <summary>
/// Base (unmodified) terrain for a world, computed offline with the ported world generator.
/// </summary>
public sealed class TerrainService
{
    /// <summary>
    /// Sea level, metres.
    /// </summary>
    public const float WaterLevel = 30f;

    /// <summary>
    /// The edge of the world, metres from the middle: the overview's reach, ocean beyond it.
    /// </summary>
    public const float MapRadius = 10500f;

    /// <summary>
    /// This world's generator (the map reads biomes and rivers from it).
    /// </summary>
    internal WorldGenerator Generator { get; }

    /// <summary>
    /// The runtime modifiers laid over generated terrain, with BaseZone's heights and BaseMask's paint by zone built
    /// with them (on first use). Swapped whole by UseModifiers, so a zone a build with the old modifiers finishes goes
    /// into the old cache, never the new one.
    /// </summary>
    /// <param name="Modifiers">The modifiers; null for none.</param>
    private sealed record Modified(TerrainEditor.Terrain.TerrainModifiers? Modifiers)
    {
        /// <summary>BaseZone's heights by zone, built with <see cref="Modifiers"/>.</summary>
        public ConcurrentDictionary<(int, int), float[]> Zones { get; } = new();

        /// <summary>BaseMask's paint masks by zone, painted by <see cref="Modifiers"/>.</summary>
        public ConcurrentDictionary<(int, int), float[]> Masks { get; } = new();
    }

    /// <summary>The modifiers in use and the zones built with them.</summary>
    private volatile Modified _ground;

    /// <summary>
    /// The overview PNG, rendered on first use.
    /// </summary>
    private readonly Lazy<byte[]> _overviewPng;

    /// <summary>
    /// New modifiers (ground discs added or taken away): the zones' ground is worked out again.
    /// </summary>
    public void UseModifiers(TerrainEditor.Terrain.TerrainModifiers modifiers)
    {
        _ground = new Modified(modifiers);
    }

    /// <summary>
    /// The terrain of a saved world: its generator is made from the save's seed and world gen version.
    /// </summary>
    /// <param name="save">The world.</param>
    /// <param name="modifiers">Runtime modifiers to lay over BaseZone's ground; null for none.</param>
    /// <param name="overviewSize">Texels per side of <see cref="OverviewPng"/>.</param>
    public TerrainService(WorldSave save, TerrainEditor.Terrain.TerrainModifiers? modifiers = null, int overviewSize = 1024)
    {
        _ground = new Modified(modifiers);
        World world = new() { m_seed = save.Seed, m_seedName = save.SeedName, m_worldGenVersion = save.WorldGenVersion };
        Generator = WorldGenerator.Create(world);
        _overviewPng = new Lazy<byte[]>(() => RenderOverview(overviewSize), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// A shaded PNG of the whole world, north up, transparent outside the world's edge; rendered on first use.
    /// </summary>
    public byte[] OverviewPng => _overviewPng.Value;

    /// <summary>
    /// The ground the game's saved edits apply to: generated terrain plus the runtime flattening of
    /// locations (TerrainModifier), exactly as Heightmap.ApplyModifiers does before TerrainComp.
    /// 65x65 vertex heights, row = z, cached; callers must not change the array.
    /// </summary>
    public float[] BaseZone(int zx, int zz)
    {
        Modified ground = _ground;
        return ground.Zones.GetOrAdd((zx, zz), k =>
        {
            float[] h = Build(k.Item1, k.Item2, null);
            ground.Modifiers?.Apply(k.Item1, k.Item2, h);
            return h;
        });
    }

    /// <summary>
    /// A zone's <see cref="BaseZone"/> and <see cref="BaseMask"/> made ready, in one generator pass when neither is
    /// cached yet: the mask comes out of the same pass as the heights, so an area's load (zones in parallel) needs
    /// no second pass for it.
    /// </summary>
    /// <param name="zx">Zone x.</param>
    /// <param name="zz">Zone z.</param>
    public void Warm(int zx, int zz)
    {
        var key = (zx, zz);
        Modified ground = _ground;
        if (ground.Masks.ContainsKey(key))
        {
            BaseZone(zx, zz);
            return;
        }
        if (ground.Zones.ContainsKey(key))
        {
            BaseMask(zx, zz);
            return;
        }
        float[] mask = new float[65 * 65 * 4];
        float[] heights = Build(zx, zz, mask);
        ground.Modifiers?.Apply(zx, zz, heights, mask);
        ground.Masks.TryAdd(key, mask);
        ground.Zones.TryAdd(key, heights);
    }

    /// <summary>How many generator passes over a zone were made (see <see cref="ZoneBuilds"/>).</summary>
    private int _builds;

    /// <summary>How many generator passes over a zone this terrain made: a test seam.</summary>
    internal int ZoneBuilds => Volatile.Read(ref _builds);

    /// <summary>One generator pass over a zone: its heights, and its base mask when one is given (see BaseTerrain).</summary>
    private float[] Build(int zx, int zz, float[]? mask)
    {
        Interlocked.Increment(ref _builds);
        return BaseTerrain.BuildZone(Generator, zx, zz, mask);
    }

    /// <summary>
    /// VertexBiomes' biomes by zone, built on first use.
    /// </summary>
    private readonly ConcurrentDictionary<(int, int), int[]> _biomes = new();

    /// <summary>
    /// Biome at every vertex of a zone (65 x 65, row = z), for brush masks.
    /// </summary>
    public int[] VertexBiomes(int zx, int zz) => _biomes.GetOrAdd((zx, zz), k =>
    {
        int[] b = new int[65 * 65];
        float x0 = k.Item1 * 64f - 32f, z0 = k.Item2 * 64f - 32f;
        for (int i = 0; i < 65; i++)
        {
            for (int j = 0; j < 65; j++)
            {
                b[i * 65 + j] = (int)Generator.GetBiome(x0 + j, z0 + i);
            }
        }
        return b;
    });

    /// <summary>
    /// The game's paint mask for a zone before the saved edits: the generated mask (biome-specific; Color.black
    /// elsewhere) with the runtime modifiers' cleared paint (dirt or paving around locations...). 65x65 RGBA floats,
    /// cached; callers must not change the array.
    /// </summary>
    public float[] BaseMask(int zx, int zz)
    {
        Modified ground = _ground;
        return ground.Masks.GetOrAdd((zx, zz), k =>
        {
            float[] m = new float[65 * 65 * 4];
            float[] h = Build(k.Item1, k.Item2, m);
            ground.Modifiers?.Apply(k.Item1, k.Item2, h, m);
            return m;
        });
    }

    /// <summary>
    /// Heightmap.m_cornerBiomes: the biome sector at each zone corner. Sectors sample GetBiome on the
    /// 12 m grid of AltBiomeWorldData (WorldSpaceToMapSpace / MapSpaceToWorldSpace).
    /// </summary>
    /// <returns>Four biomes: corners (-x, -z), (+x, -z), (-x, +z), (+x, +z); Ocean beyond the world's edge.</returns>
    public int[] CornerBiomes(int zx, int zz)
    {
        static float Snap(float v) => SeedLocations.BiomeMap.MapToWorld(SeedLocations.BiomeMap.WorldToMap(v));
        float x0 = zx * 64f - 32f, z0 = zz * 64f - 32f;
        return [.. new[] { (x0, z0), (x0 + 64f, z0), (x0, z0 + 64f), (x0 + 64f, z0 + 64f) }.Select(c => { float sx = Snap(c.Item1), sz = Snap(c.Item2); return sx * sx + sz * sz > MapRadius * MapRadius ? (int)Heightmap.Biome.Ocean : (int)Generator.GetBiome(sx, sz); })];
    }

    /// <summary>
    /// Generated terrain only, without location flattening (not cached).
    /// </summary>
    public float[] RawZone(int zx, int zz) => Build(zx, zz, null);

    /// <summary>
    /// The biome at a world point.
    /// </summary>
    public Heightmap.Biome BiomeAt(float x, float z) => Generator.GetBiome(x, z);

    /// <summary>
    /// Renders the overview PNG: biome colours, water by depth, hill shading on land.
    /// </summary>
    private byte[] RenderOverview(int size)
    {
        float[] heights = new float[size * size];
        var biomes = new Heightmap.Biome[size * size];
        float step = 2f * MapRadius / size;
        // Row 0 is the north edge so the PNG has north up.
        Parallel.For(0, size, row =>
        {
            float z = MapRadius - (row + 0.5f) * step;
            for (int col = 0; col < size; col++)
            {
                float x = -MapRadius + (col + 0.5f) * step;
                int i = row * size + col;
                if (x * x + z * z > MapRadius * MapRadius)
                {
                    heights[i] = -400f;
                    biomes[i] = Heightmap.Biome.Ocean;
                    continue;
                }
                Heightmap.Biome biome = Generator.GetBiome(x, z);
                biomes[i] = biome;
                heights[i] = Generator.GetBiomeHeight(biome, x, z, out _);
            }
        });
        byte[] rgba = new byte[size * size * 4];
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                int i = row * size + col;
                float h = heights[i];
                (float r, float g, float b) = BiomeColor(biomes[i], h);
                if (h >= WaterLevel)
                {
                    // Simple hill shading from the north-west.
                    float dx = heights[row * size + Math.Min(col + 1, size - 1)] - heights[row * size + Math.Max(col - 1, 0)];
                    float dz = heights[Math.Min(row + 1, size - 1) * size + col] - heights[Math.Max(row - 1, 0) * size + col];
                    float shade = Math.Clamp(1f - (dx - dz) * 0.012f, 0.55f, 1.35f);
                    r *= shade;
                    g *= shade;
                    b *= shade;
                }
                rgba[i * 4] = (byte)Math.Clamp(r, 0f, 255f);
                rgba[i * 4 + 1] = (byte)Math.Clamp(g, 0f, 255f);
                rgba[i * 4 + 2] = (byte)Math.Clamp(b, 0f, 255f);
                rgba[i * 4 + 3] = (byte)(heights[i] <= -400f ? 0 : 255);
            }
        }
        return Png.Encode(size, size, rgba);
    }

    /// <summary>
    /// The overview's colour (0..255 per channel) for a biome at a height: blue, darker with depth, below sea level.
    /// </summary>
    internal static (float, float, float) BiomeColor(Heightmap.Biome biome, float h)
    {
        if (h < WaterLevel)
        {
            float depth = Math.Clamp((WaterLevel - h) / 60f, 0f, 1f);
            return (40 - 25 * depth, 95 - 55 * depth, 140 - 50 * depth);
        }
        return biome switch
        {
            Heightmap.Biome.Meadows => (112, 158, 72),
            Heightmap.Biome.BlackForest => (58, 92, 54),
            Heightmap.Biome.Swamp => (110, 94, 68),
            Heightmap.Biome.Mountain => (205, 210, 216),
            Heightmap.Biome.Plains => (196, 178, 96),
            Heightmap.Biome.Mistlands => (102, 98, 116),
            Heightmap.Biome.AshLands => (150, 64, 48),
            Heightmap.Biome.DeepNorth => (220, 232, 240),
            _ => (150, 150, 150)
        };
    }
}

/// <summary>
/// Minimal PNG writer (8-bit RGBA, no filtering).
/// </summary>
public static class Png
{
    /// <summary>
    /// The CRC-32 table PNG chunks are checked with.
    /// </summary>
    private static readonly uint[] CrcTable = [.. Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }
        return c;
    })];

    /// <summary>
    /// A PNG file of an image, rows top first, 4 bytes per pixel.
    /// </summary>
    public static byte[] Encode(int width, int height, byte[] rgba)
    {
        using MemoryStream png = new();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        byte[] ihdr = new byte[13];
        WriteBE(ihdr, 0, (uint)width);
        WriteBE(ihdr, 4, (uint)height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(png, "IHDR", ihdr);
        using (MemoryStream raw = new())
        {
            using (ZLibStream z = new(raw, CompressionLevel.Fastest, leaveOpen: true))
            {
                for (int y = 0; y < height; y++)
                {
                    z.WriteByte(0);
                    z.Write(rgba, y * width * 4, width * 4);
                }
            }
            Chunk(png, "IDAT", raw.ToArray());
        }
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>
    /// Writes one chunk: length, type, data and CRC.
    /// </summary>
    private static void Chunk(Stream s, string type, byte[] data)
    {
        byte[] len = new byte[4];
        WriteBE(len, 0, (uint)data.Length);
        s.Write(len);
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in typeBytes.Concat(data))
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        WriteBE(len, 0, crc ^ 0xFFFFFFFFu);
        s.Write(len);
    }

    /// <summary>
    /// Writes v big-endian at b[o].
    /// </summary>
    private static void WriteBE(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }
}

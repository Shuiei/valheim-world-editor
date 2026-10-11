using System.Collections.Concurrent;
using TerrainEditor.Editing;

namespace ValheimGen;

/// <summary>
/// The inputs of Valheim's world map shader (Minimap.GenerateWorldMap): a biome colour texture, a
/// mask texture (r forest, g mist, b ashlands lava/ocean) and a height texture. The overview uses the
/// game's own layout (2048 texels of 12 m); detail windows use 1 m texels and include terrain edits.
/// </summary>
public sealed class MapData
{
    /// <summary>
    /// Texels per side of the overview, as the game's own map.
    /// </summary>
    public const int GlobalSize = 2048;

    /// <summary>
    /// Metres per overview texel.
    /// </summary>
    public const float GlobalPixel = 12f;

    /// <summary>
    /// Colours from the Minimap component in the game's assets (Mistlands keeps its code default).
    /// </summary>
    private static readonly (Heightmap.Biome Biome, byte R, byte G, byte B)[] BiomeColors =
    [
        (Heightmap.Biome.Meadows, Byte(0.5725338f), Byte(0.6551003f), Byte(0.3605249f)),
        (Heightmap.Biome.AshLands, Byte(0.4811321f), Byte(0.124822f), Byte(0.124822f)),
        (Heightmap.Biome.BlackForest, Byte(0.4196384f), Byte(0.4548104f), Byte(0.2466926f)),
        (Heightmap.Biome.DeepNorth, 255, 255, 255),
        (Heightmap.Biome.Plains, Byte(0.9062028f), Byte(0.6707256f), Byte(0.4704356f)),
        (Heightmap.Biome.Swamp, Byte(0.6394598f), Byte(0.4469825f), Byte(0.3448844f)),
        (Heightmap.Biome.Mountain, 255, 255, 255),
        (Heightmap.Biome.Mistlands, Byte(0.2f), Byte(0.2f), Byte(0.2f)),
    ];
    /// <summary>
    /// Base (unedited) terrain heights by zone, for detail windows.
    /// </summary>
    private readonly TerrainService _terrain;

    /// <summary>
    /// The world's terrain edits, laid over the base heights in detail windows.
    /// </summary>
    private readonly EditStore _edits;

    /// <summary>
    /// The overview, built once on first use.
    /// </summary>
    private readonly Lazy<Layers> _global;

    /// <summary>
    /// Detail windows by position, size and edit version; dropped all at once past a handful.
    /// </summary>
    private readonly ConcurrentDictionary<string, Layers> _details = new();

    /// <summary>
    /// Map data for the world whose generator <paramref name="terrain"/> uses.
    /// </summary>
    public MapData(TerrainService terrain, EditStore edits)
    {
        _terrain = terrain;
        _edits = edits;
        Generator = terrain.Generator;
        _global = new Lazy<Layers>(BuildGlobal, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// The world's generator (the terrain service's).
    /// </summary>
    internal WorldGenerator Generator { get; }

    /// <summary>
    /// The whole-world overview (<see cref="GlobalSize"/> texels of <see cref="GlobalPixel"/> m), built on first
    /// use; base terrain only, no edits.
    /// </summary>
    public Layers Global => _global.Value;

    /// <summary>
    /// A 0..1 value as a byte, rounded and held to 0..255.
    /// </summary>
    private static byte Byte(float v) => (byte)Math.Clamp((int)Math.Round(v * 255f), 0, 255);

    /// <summary>
    /// A biome's colour on the map; white for any biome not in the game's list (and for combined flags).
    /// </summary>
    public static (byte, byte, byte) PixelColor(Heightmap.Biome biome)
    {
        foreach (var (kind, r, g, b) in BiomeColors)
        {
            if (kind == biome)
            {
                return (r, g, b);
            }
        }
        return (255, 255, 255);
    }

    /// <summary>
    /// The mask texel at a world point (r forest, g mist, b ashlands lava/ocean). Mirrors Minimap.GetMaskColor.
    /// </summary>
    private (byte, byte, byte) MaskColor(float wx, float wy, float height, Heightmap.Biome biome)
    {
        if (height < 30f)
        {
            return (0, 0, Byte(Math.Clamp(WorldGenerator.GetAshlandsOceanGradient(wx, wy), 0f, 1f)));
        }
        Vector3 p = new(wx, 0f, wy);
        switch (biome)
        {
            case Heightmap.Biome.Meadows:
                return WorldGenerator.InForest(p) ? ((byte)255, (byte)0, (byte)0) : ((byte)0, (byte)0, (byte)0);
            case Heightmap.Biome.Plains:
                return WorldGenerator.GetForestFactor(p) < 0.8f ? ((byte)255, (byte)0, (byte)0) : ((byte)0, (byte)0, (byte)0);
            case Heightmap.Biome.BlackForest:
                return (255, 0, 0);
            case Heightmap.Biome.Mistlands:
            {
                float f = WorldGenerator.GetForestFactor(p);
                float t = Math.Clamp((f - 1.1f) / 0.2f, 0f, 1f);
                return (0, Byte(1f - t * t * (3f - 2f * t)), 0);
            }
            case Heightmap.Biome.AshLands:
                Generator.GetAshlandsHeight(wx, wy, out Color mask, cheap: true);
                return (0, 0, Byte(mask.a));
            default:
                return (0, 0, 0);
        }
    }

    /// <summary>
    /// Builds the overview from the generator alone, rows in parallel.
    /// </summary>
    private Layers BuildGlobal()
    {
        int n = GlobalSize;
        Layers layers = new(n, n);
        int half = n / 2;
        float pix = GlobalPixel;
        // Same sample positions as Minimap.GenerateWorldMap; row i is world z. All the cores but one: unbounded,
        // the loop took every thread of the pool for as long as it ran (seconds), so nothing else that needs
        // one ran meanwhile (an HTTP answer from the game, a timer).
        Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
        {
            float wy = (i - half) * pix + pix / 2f;
            for (int j = 0; j < n; j++)
            {
                float wx = (j - half) * pix + pix / 2f;
                Heightmap.Biome biome = Generator.GetBiome(wx, wy);
                float h = Generator.GetBiomeHeight(biome, wx, wy, out _);
                layers.Set(i * n + j, h, PixelColor(biome), MaskColor(wx, wy, h, biome), null);
            }
        });
        return layers;
    }

    /// <summary>
    /// A 1 m detail window whose texel (0, 0) is the world vertex (x0, z0). Includes terrain edits.
    /// </summary>
    /// <param name="x0">World x of the first column, metres.</param>
    /// <param name="z0">World z of the first row, metres.</param>
    /// <param name="size">Texels per side.</param>
    /// <param name="editVersion">The edits' version: a new one builds the window again instead of using the cached
    /// one.</param>
    public Layers Detail(int x0, int z0, int size, int editVersion)
    {
        string key = $"{x0},{z0},{size},{editVersion}";
        if (_details.Count > 6)
        {
            _details.Clear();
        }
        return _details.GetOrAdd(key, _ => BuildDetail(x0, z0, size));
    }

    /// <summary>
    /// Builds a detail window: each zone's base heights with its height and paint edits laid over them.
    /// </summary>
    private Layers BuildDetail(int x0, int z0, int size)
    {
        Layers layers = new(size, size);
        // Gather the zones the window touches: base terrain and current edits.
        int zx0 = (int)Math.Floor((x0 + 32) / 64.0), zx1 = (int)Math.Floor((x0 + size - 1 + 32) / 64.0);
        int zz0 = (int)Math.Floor((z0 + 32) / 64.0), zz1 = (int)Math.Floor((z0 + size - 1 + 32) / 64.0);
        var zones = (from zz in Enumerable.Range(zz0, zz1 - zz0 + 1) from zx in Enumerable.Range(zx0, zx1 - zx0 + 1) select (zx, zz)).ToList();
        var bases = new ConcurrentDictionary<(int, int), float[]>();
        var edits = new ConcurrentDictionary<(int, int), ZoneEdit?>();
        Parallel.ForEach(zones, k =>
        {
            bases[k] = _terrain.BaseZone(k.zx, k.zz);
            edits[k] = _edits.Get(k.zx, k.zz);
        });
        Parallel.For(0, size, row =>
        {
            int wz = z0 + row;
            int zz = (int)Math.Floor((wz + 32) / 64.0);
            int k = wz - (zz * 64 - 32);
            for (int col = 0; col < size; col++)
            {
                int wx = x0 + col;
                int zx = (int)Math.Floor((wx + 32) / 64.0);
                int l = wx - (zx * 64 - 32);
                int vi = k * EditStore.Grid + l;
                float h = bases[(zx, zz)][vi];
                ZoneEdit? e = edits[(zx, zz)];
                (byte, byte, byte, byte)? paint = null;
                if (e != null)
                {
                    if (e.Modified[vi])
                    {
                        // TerrainComp.ApplyToHeightmap: never more than 8 m from the ground below.
                        h = Math.Clamp(h + e.Level[vi] + e.Smooth[vi], h - 8f, h + 8f);
                    }
                    if (e.PaintModified[vi])
                    {
                        paint = (Byte(e.Paint[vi * 4]), Byte(e.Paint[vi * 4 + 1]), Byte(e.Paint[vi * 4 + 2]), Byte(e.Paint[vi * 4 + 3]));
                    }
                }
                Heightmap.Biome biome = Generator.GetBiome(wx, wz);
                layers.Set(row * size + col, h, PixelColor(biome), MaskColor(wx, wz, h, biome), paint);
            }
        });
        return layers;
    }

    /// <summary>
    /// The map textures of one window, row by row (row is world z, column world x).
    /// </summary>
    /// <param name="width">Texels per row.</param>
    /// <param name="height">Rows.</param>
    public sealed class Layers(int width, int height)
    {
        /// <summary>
        /// Texels per row.
        /// </summary>
        public int Width { get; } = width;

        /// <summary>
        /// Number of rows.
        /// </summary>
        public int Height { get; } = height;

        /// <summary>
        /// Ground height per texel, metres.
        /// </summary>
        public Half[] Heights { get; } = new Half[width * height];

        /// <summary>
        /// Biome colour per texel (RGBA8, a always 255).
        /// </summary>
        public byte[] Biome { get; } = new byte[width * height * 4];

        /// <summary>
        /// Mask per texel (RGBA8: r forest, g mist, b ashlands lava/ocean, a always 255).
        /// </summary>
        public byte[] Mask { get; } = new byte[width * height * 4];

        /// <summary>
        /// Painted ground (r dirt, g cultivated, b paved, a = 255 where painted).
        /// </summary>
        public byte[] Paint { get; } = new byte[width * height * 4];

        /// <summary>
        /// Fills texel <paramref name="i"/>.
        /// </summary>
        /// <param name="i">The texel's index (row * width + column).</param>
        /// <param name="h">Ground height, metres.</param>
        /// <param name="biome">Biome colour.</param>
        /// <param name="mask">Mask colour.</param>
        /// <param name="paint">Painted ground; null leaves the texel unpainted. Its alpha is not used: a painted
        /// texel always gets 255.</param>
        public void Set(int i, float h, (byte R, byte G, byte B) biome, (byte R, byte G, byte B) mask, (byte R, byte G, byte B, byte A)? paint)
        {
            Heights[i] = (Half)h;
            Biome[i * 4] = biome.R;
            Biome[i * 4 + 1] = biome.G;
            Biome[i * 4 + 2] = biome.B;
            Biome[i * 4 + 3] = 255;
            Mask[i * 4] = mask.R;
            Mask[i * 4 + 1] = mask.G;
            Mask[i * 4 + 2] = mask.B;
            Mask[i * 4 + 3] = 255;
            if (paint is { } p)
            {
                Paint[i * 4] = p.R;
                Paint[i * 4 + 1] = p.G;
                Paint[i * 4 + 2] = p.B;
                Paint[i * 4 + 3] = 255;
            }
        }

        /// <summary>
        /// Binary blob for the browser: heights (float16, little-endian) then biome, mask and paint (RGBA8 each).
        /// </summary>
        public byte[] ToBytes()
        {
            int n = Width * Height;
            byte[] result = new byte[n * 2 + n * 12];
            for (int i = 0; i < n; i++)
            {
                ushort bits = BitConverter.HalfToUInt16Bits(Heights[i]);
                result[i * 2] = (byte)bits;
                result[i * 2 + 1] = (byte)(bits >> 8);
            }
            Buffer.BlockCopy(Biome, 0, result, n * 2, n * 4);
            Buffer.BlockCopy(Mask, 0, result, n * 6, n * 4);
            Buffer.BlockCopy(Paint, 0, result, n * 10, n * 4);
            return result;
        }
    }
}

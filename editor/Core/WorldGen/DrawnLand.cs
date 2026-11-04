// Shared by the editor (ValheimWorldEditor.Core) and the game's mod (editor/plugin/DrawnWorlds), which compiles this
// very file: both must work out the same land from the same map, to the bit, or the ground the editor shows and edits
// would not be the ground the game makes. So it holds nothing of either (no Unity, no editor types), works in double
// (whose + - * / and square root give the same bits on .NET and on the game's Mono) and rounds to float only where it
// stores a value, and takes the game's noise as a function the caller gives (Unity's Mathf.PerlinNoise, which the
// editor matches).
using System.IO.Compression;

namespace DrawnWorlds;

/// <summary>
/// The land a master map draws: each cell's kind of ground (the legend's order: Meadows 0, Black Forest 1, Swamp 2,
/// Mountain 3, Plains 4, Mistlands 5, Ashlands 6, Deep North 7, River 8, Lake 9, Sea 10, Deep sea 11), turned into
/// the continents' height (the game's GetBaseHeight, in its units: the sea's surface at 0.15) and the kind whose
/// biome each point is in. Distance fields over the cells give each kind its height (coasts rising from the sea,
/// foothills rising to the mountains, mountains higher the further in, the sea deeper the further out), then a light
/// blur; a river or a lake takes the kind of its banks for its biome.
/// </summary>
public sealed class DrawnLand
{
    /// <summary>Cells on a side of every master map.</summary>
    public const int Size = 2048;

    /// <summary>Half the square a map covers, metres (the game's world radius).</summary>
    public const double Half = 10000.0;

    /// <summary>The kinds the land reads.</summary>
    public const byte Meadows = 0, BlackForest = 1, Swamp = 2, Mountain = 3, Plains = 4, Mistlands = 5, AshLands = 6,
        DeepNorth = 7, River = 8, Lake = 9, Sea = 10, DeepSea = 11, Kinds = 12;

    /// <summary>The continents' height at the coast (just under the game's waterline, 0.15).</summary>
    private const double Coast = 0.13;

    /// <summary>The continents' height where the mountains begin (the game's Mountain threshold).</summary>
    private const double MountainFoot = 0.4;

    /// <summary>Metres a cell.</summary>
    private const double Cell = 2.0 * Half / Size;

    /// <summary>The continents' height at each cell's middle.</summary>
    private readonly float[] _land;

    /// <summary>The kind whose biome each cell is in (a river's or a lake's: its banks').</summary>
    private readonly byte[] _biome;

    /// <summary>The noise the biomes' edges wander by (Unity's Mathf.PerlinNoise).</summary>
    private readonly Func<float, float, float> _noise;

    /// <summary>The land of a map's cells.</summary>
    /// <param name="cells"><see cref="Size"/> × <see cref="Size"/> kinds, row by row from the north, west first.</param>
    /// <param name="noise">Unity's Mathf.PerlinNoise (or the editor's copy of it).</param>
    public DrawnLand(byte[] cells, Func<float, float, float> noise)
        : this(cells, null, noise)
    {
    }

    /// <summary>
    /// The land of a map's cells, with the heights of a heightmap when there are some: each cell's continents are then
    /// the heightmap's, held to what its kind needs (painted land above the water, mountains at the game's mountain
    /// height or above, painted water below the land's own water).
    /// </summary>
    /// <param name="cells"><see cref="Size"/> × <see cref="Size"/> kinds, row by row from the north, west first.</param>
    /// <param name="heights">The continents at each cell (<see cref="HeightOf"/>), or null.</param>
    /// <param name="noise">Unity's Mathf.PerlinNoise (or the editor's copy of it).</param>
    public DrawnLand(byte[] cells, ushort[]? heights, Func<float, float, float> noise)
    {
        if (heights != null && heights.Length != Size * Size)
        {
            throw new ArgumentException("a master map's heights are " + Size + " × " + Size, nameof(heights));
        }
        if (cells.Length != Size * Size)
        {
            throw new ArgumentException("a master map has " + Size + " × " + Size + " cells", nameof(cells));
        }
        _noise = noise;
        int count = Size * Size;
        // Distances, in cells: from land to the nearest water, from water to the nearest land (and which), from the
        // foot of the mountains and from inside them to their edge, and from the deep sea to shallower water.
        float[] toWater = Distances(cells, k => k >= River, out _);
        float[] toLand = Distances(cells, k => k < River, out int[] nearestLand);
        float[] toMountain = Distances(cells, k => k == Mountain, out _);
        float[] toLowland = Distances(cells, k => k != Mountain, out _);
        float[] toShallow = Distances(cells, k => k != DeepSea, out _);
        float[] raw = new float[count];
        _biome = new byte[count];
        for (int i = 0; i < count; i++)
        {
            byte k = cells[i];
            double v;
            if (k == Mountain)
            {
                v = MountainFoot + 0.35 * Ease(toLowland[i] * Cell / 2000.0);
            }
            else if (k < River)
            {
                v = Coast + (Inland(k) - Coast) * Ease(toWater[i] * Cell / 500.0);
                double foothills = 1.0 - Ease(toMountain[i] * Cell / 800.0);
                if (v < MountainFoot)
                {
                    v += (MountainFoot - v) * foothills * foothills;
                }
            }
            else if (k == Sea)
            {
                v = Coast + (-0.02 - Coast) * Ease(toLand[i] * Cell / 500.0);
            }
            else if (k == DeepSea)
            {
                double shallow = Coast + (-0.02 - Coast) * Ease(toLand[i] * Cell / 500.0);
                v = shallow + (-0.25 - shallow) * Ease(toShallow[i] * Cell / 1500.0);
            }
            else if (k == Lake)
            {
                v = 0.12 + (0.0 - 0.12) * Ease(toLand[i] * Cell / 200.0);
            }
            else
            {
                v = 0.03;
            }
            if (heights != null)
            {
                double h = ContinentOf(heights[i]);
                v = k == Mountain ? Math.Max(h, MountainFoot + 0.01)
                    : k < River ? Math.Max(h, Coast + 0.04)
                    : Math.Min(h, v);
            }
            raw[i] = (float)v;
            _biome[i] = k is River or Lake ? cells[nearestLand[i]] is var bank && bank < River ? bank : Meadows : k;
        }
        // Softened (two passes of a 5-cell box, about 50 m), the rivers and lakes kept as deep as drawn.
        _land = Blur(Blur(raw, 2), 2);
        for (int i = 0; i < count; i++)
        {
            if (cells[i] is River or Lake && raw[i] < _land[i])
            {
                _land[i] = raw[i];
            }
        }
    }

    /// <summary>The continents' height a land kind rises to inland, past the coast.</summary>
    private static double Inland(byte kind) => kind switch
    {
        BlackForest => 0.30,
        Swamp => 0.16, // the game keeps its ground at the waterline whatever the continents
        Plains => 0.22, // flat
        Mistlands => 0.26, // its peaks rise from there
        AshLands => 0.22,
        DeepNorth => 0.16, // the game raises it by 0.1
        _ => 0.24,
    };

    /// <summary>The lowest and highest continents a height keeps (the game's sea floor to far above its peaks).</summary>
    private const double LowestHeight = -0.5, HighestHeight = 1.5;

    /// <summary>A continents' height kept as a heightmap's cell (16 bits from <see cref="LowestHeight"/> to <see cref="HighestHeight"/>).</summary>
    public static ushort HeightOf(double continent)
    {
        double t = (continent - LowestHeight) / (HighestHeight - LowestHeight);
        return (ushort)Math.Round(Math.Max(0.0, Math.Min(1.0, t)) * 65535.0);
    }

    /// <summary>The continents' height a heightmap's cell keeps.</summary>
    public static double ContinentOf(ushort height) => LowestHeight + height * ((HighestHeight - LowestHeight) / 65535.0);

    /// <summary>0 to 1, eased at both ends (smoothstep), held to 0..1.</summary>
    private static double Ease(double t)
    {
        t = t < 0.0 ? 0.0 : t > 1.0 ? 1.0 : t;
        return t * t * (3.0 - 2.0 * t);
    }

    /// <summary>
    /// The continents' height at a point (the game's GetBaseHeight inside the world; the caller sinks it past the
    /// edge): between the four nearest cells' middles, held to the picture's edge.
    /// </summary>
    /// <param name="x">World x (east).</param>
    /// <param name="z">World z (north).</param>
    public float Continent(float x, float z)
    {
        double fx = ((double)x + Half) / Cell - 0.5, fy = (Half - (double)z) / Cell - 0.5;
        fx = fx < 0.0 ? 0.0 : fx > Size - 1 ? Size - 1 : fx;
        fy = fy < 0.0 ? 0.0 : fy > Size - 1 ? Size - 1 : fy;
        int x0 = (int)fx, y0 = (int)fy;
        int x1 = x0 + 1 < Size ? x0 + 1 : x0, y1 = y0 + 1 < Size ? y0 + 1 : y0;
        double tx = fx - x0, ty = fy - y0;
        double a = _land[y0 * Size + x0], b = _land[y0 * Size + x1], c = _land[y1 * Size + x0], d = _land[y1 * Size + x1];
        double top = a + (b - a) * tx, bottom = c + (d - c) * tx;
        return (float)(top + (bottom - top) * ty);
    }

    /// <summary>
    /// The kind whose biome a point is in: its cell's, read a little off the point (by up to 12 m, by noise) so that
    /// the edges between biomes wander instead of following the cells' steps.
    /// </summary>
    /// <param name="x">World x (east).</param>
    /// <param name="z">World z (north).</param>
    public byte BiomeKind(float x, float z)
    {
        double nx = _noise((float)((double)x * 0.02 + 731.0), (float)((double)z * 0.02 + 113.0));
        double nz = _noise((float)((double)x * 0.02 + 257.0), (float)((double)z * 0.02 + 919.0));
        double wx = (double)x + (nx - 0.5) * 24.0, wz = (double)z + (nz - 0.5) * 24.0;
        int col = (int)Math.Floor((wx + Half) / Cell), row = (int)Math.Floor((Half - wz) / Cell);
        col = col < 0 ? 0 : col >= Size ? Size - 1 : col;
        row = row < 0 ? 0 : row >= Size ? Size - 1 : row;
        return _biome[row * Size + col];
    }

    /// <summary>
    /// The straight-line distance (in cells) from each cell to the nearest cell that <paramref name="feature"/> holds
    /// for (0 on those), and which that cell is: the exact Euclidean distance transform, by columns then by rows
    /// (Felzenszwalb and Huttenlocher's lower envelope of parabolas). Far when no cell is a feature.
    /// </summary>
    private static float[] Distances(byte[] cells, Func<byte, bool> feature, out int[] nearest)
    {
        const double far = 1e12;
        int n = Size, count = n * n;
        double[] d2 = new double[count];
        nearest = new int[count];
        bool[] isFeature = new bool[256];
        for (int k = 0; k < 256; k++)
        {
            isFeature[k] = feature((byte)k);
        }
        for (int i = 0; i < count; i++)
        {
            d2[i] = isFeature[cells[i]] ? 0.0 : far;
            nearest[i] = i;
        }
        double[] f = new double[n], outD = new double[n], z = new double[n + 1];
        int[] fIdx = new int[n], outIdx = new int[n], v = new int[n];
        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < n; y++)
            {
                f[y] = d2[y * n + x];
                fIdx[y] = nearest[y * n + x];
            }
            Envelope(f, fIdx, n, outD, outIdx, v, z);
            for (int y = 0; y < n; y++)
            {
                d2[y * n + x] = outD[y];
                nearest[y * n + x] = outIdx[y];
            }
        }
        for (int y = 0; y < n; y++)
        {
            Array.Copy(d2, y * n, f, 0, n);
            Array.Copy(nearest, y * n, fIdx, 0, n);
            Envelope(f, fIdx, n, outD, outIdx, v, z);
            Array.Copy(outD, 0, d2, y * n, n);
            Array.Copy(outIdx, 0, nearest, y * n, n);
        }
        float[] d = new float[count];
        for (int i = 0; i < count; i++)
        {
            d[i] = d2[i] >= far ? (float)far : (float)Math.Sqrt(d2[i]);
        }
        return d;
    }

    /// <summary>One line of the distance transform: out[q] = min over p of (q - p)² + f[p], and the feature p carried.</summary>
    private static void Envelope(double[] f, int[] fIdx, int n, double[] outD, int[] outIdx, int[] v, double[] z)
    {
        const double far = 1e12;
        int k = -1;
        for (int q = 0; q < n; q++)
        {
            if (f[q] >= far)
            {
                continue;
            }
            double s = 0.0;
            while (k >= 0)
            {
                int p = v[k];
                s = (f[q] + (double)q * q - (f[p] + (double)p * p)) / (2.0 * (q - p));
                if (s > z[k])
                {
                    break;
                }
                k--;
            }
            k++;
            v[k] = q;
            z[k] = k == 0 ? double.NegativeInfinity : s;
            z[k + 1] = double.PositiveInfinity;
        }
        if (k < 0)
        {
            for (int q = 0; q < n; q++)
            {
                outD[q] = far;
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
            outD[q] = (double)(q - p) * (q - p) + f[p];
            outIdx[q] = fIdx[p];
        }
    }

    /// <summary>A box blur of radius <paramref name="r"/> cells, across then down (held at the picture's edges).</summary>
    private static float[] Blur(float[] src, int r)
    {
        int n = Size;
        float[] across = new float[src.Length], down = new float[src.Length];
        double w = 1.0 / (2 * r + 1);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                double sum = 0.0;
                for (int d = -r; d <= r; d++)
                {
                    int xx = x + d < 0 ? 0 : x + d >= n ? n - 1 : x + d;
                    sum += src[y * n + xx];
                }
                across[y * n + x] = (float)(sum * w);
            }
        }
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                double sum = 0.0;
                for (int d = -r; d <= r; d++)
                {
                    int yy = y + d < 0 ? 0 : y + d >= n ? n - 1 : y + d;
                    sum += across[yy * n + x];
                }
                down[y * n + x] = (float)(sum * w);
            }
        }
        return down;
    }

    // ---- The map as a world file keeps it.

    /// <summary>The newest version of how a map is kept in a world file (<see cref="Encode"/>); 1 has no heights.</summary>
    public const int FormatVersion = 2;

    /// <summary>
    /// A map as a world file keeps it: the format's version (1 without heights, 2 with), the side, the start cell
    /// (column, row; -1, -1 for none), the cells deflated (a drawing of large areas of one kind shrinks to a few dozen KB),
    /// then in version 2 the heights deflated (two bytes a cell, low first).
    /// </summary>
    public static byte[] Encode(byte[] cells, int startColumn, int startRow, ushort[]? heights = null)
    {
        using var o = new MemoryStream();
        using var w = new BinaryWriter(o);
        w.Write(heights == null ? 1 : 2);
        w.Write(Size);
        w.Write(startColumn);
        w.Write(startRow);
        byte[] packed = Deflate(cells);
        w.Write(packed.Length);
        w.Write(packed);
        if (heights != null)
        {
            byte[] bytes = new byte[heights.Length * 2];
            for (int i = 0; i < heights.Length; i++)
            {
                bytes[2 * i] = (byte)heights[i];
                bytes[2 * i + 1] = (byte)(heights[i] >> 8);
            }
            packed = Deflate(bytes);
            w.Write(packed.Length);
            w.Write(packed);
        }
        w.Flush();
        return o.ToArray();
    }

    /// <summary>A map kept by <see cref="Encode"/>: its cells, its start cell (-1, -1 for none) and its heights (or null).</summary>
    /// <exception cref="InvalidDataException">A newer format, another size, data cut short, or a cell that is no kind.</exception>
    public static byte[] Decode(byte[] kept, out int startColumn, out int startRow, out ushort[]? heights)
    {
        using var r = new BinaryReader(new MemoryStream(kept));
        int version = r.ReadInt32();
        if (version is < 1 or > FormatVersion)
        {
            throw new InvalidDataException("the drawn world's map is of format " + version + ", this one reads up to " + FormatVersion + " (a newer editor made it)");
        }
        if (r.ReadInt32() != Size)
        {
            throw new InvalidDataException("the drawn world's map is not " + Size + " cells across");
        }
        startColumn = r.ReadInt32();
        startRow = r.ReadInt32();
        byte[] cells = Inflate(r.ReadBytes(r.ReadInt32()), Size * Size);
        if (Array.FindIndex(cells, c => c >= Kinds) >= 0)
        {
            throw new InvalidDataException("the drawn world's map has a cell of no kind");
        }
        heights = null;
        if (version >= 2)
        {
            byte[] bytes = Inflate(r.ReadBytes(r.ReadInt32()), Size * Size * 2);
            heights = new ushort[Size * Size];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = (ushort)(bytes[2 * i] | (bytes[2 * i + 1] << 8));
            }
        }
        return cells;
    }

    /// <summary>The same, for a caller that needs no heights.</summary>
    public static byte[] Decode(byte[] kept, out int startColumn, out int startRow) => Decode(kept, out startColumn, out startRow, out _);

    /// <summary>Bytes deflated.</summary>
    private static byte[] Deflate(byte[] bytes)
    {
        using var packed = new MemoryStream();
        using (var z = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(bytes, 0, bytes.Length);
        }
        return packed.ToArray();
    }

    /// <summary>Deflated bytes back, exactly <paramref name="length"/> of them.</summary>
    /// <exception cref="InvalidDataException">Fewer.</exception>
    private static byte[] Inflate(byte[] packed, int length)
    {
        byte[] bytes = new byte[length];
        using var z = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
        int got = 0, n;
        while (got < length && (n = z.Read(bytes, got, length - got)) > 0)
        {
            got += n;
        }
        if (got != length)
        {
            throw new InvalidDataException("the drawn world's map is cut short");
        }
        return bytes;
    }

    /// <summary>The middle of a cell in the world (x east, z north).</summary>
    public static void CenterOf(int column, int row, out float x, out float z)
    {
        x = (float)(-Half + (column + 0.5) * Cell);
        z = (float)(Half - (row + 0.5) * Cell);
    }
}

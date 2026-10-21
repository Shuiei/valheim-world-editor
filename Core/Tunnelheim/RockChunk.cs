namespace TerrainEditor.Tunnelheim;

/// <summary>
/// One 16 m cube of a column's rock: 32 samples a side, each the height of rock above it (positive inside rock), kept
/// to 1/256 m within ±4 m of the surface, and a paint per sample. A chunk nothing has changed keeps no array and reads
/// as its column's seed ground.
/// </summary>
public sealed class RockChunk
{
    private readonly int _baseY;
    private float[] _seeds;
    private short[]? _density;
    private byte[]? _paint;

    /// <summary>A chunk of seed ground.</summary>
    /// <param name="seeds">Its column's ground heights (<see cref="ColumnRecord.SeedCount"/> of them).</param>
    /// <param name="y">Its height index in the column.</param>
    public RockChunk(float[] seeds, int y)
    {
        _seeds = seeds;
        _baseY = y * RockGrid.Samples;
    }

    /// <summary>Whether a sample was ever changed (the chunk keeps its own values).</summary>
    public bool IsStored => _density != null;

    /// <summary>Whether any sample was painted.</summary>
    public bool IsPainted => _paint != null;

    /// <summary>A sample's density, in metres (positive inside rock).</summary>
    /// <param name="i">The sample's index (x fastest, then y, then z).</param>
    public float this[int i]
    {
        get => (_density != null ? _density[i] : SeedStep(i)) / 256f;
        set
        {
            short v = Encode(value);
            if (_density == null)
            {
                if (v == SeedStep(i))
                {
                    return;
                }
                _density = new short[RockGrid.ChunkSamples];
                for (int k = 0; k < _density.Length; k++)
                {
                    _density[k] = SeedStep(k);
                }
            }
            _density[i] = v;
        }
    }

    /// <summary>A sample's density as stored, in 1/256 m.</summary>
    /// <param name="i">The sample's index.</param>
    public short Raw(int i) => _density != null ? _density[i] : SeedStep(i);

    /// <summary>A sample's paint (<see cref="RockPaint"/>).</summary>
    /// <param name="i">The sample's index.</param>
    public byte PaintAt(int i) => _paint?[i] ?? 0;

    /// <summary>Paints a sample.</summary>
    /// <param name="i">The sample's index.</param>
    /// <param name="code">The paint (<see cref="RockPaint"/>).</param>
    public void Paint(int i, byte code)
    {
        if (_paint == null)
        {
            if (code == 0)
            {
                return;
            }
            _paint = new byte[RockGrid.ChunkSamples];
        }
        _paint[i] = code;
    }

    /// <summary>Takes a checkpoint's paint for the whole chunk.</summary>
    /// <param name="paint">One paint per sample.</param>
    public void TakePaint(byte[] paint)
    {
        for (int i = 0; i < paint.Length; i++)
        {
            Paint(i, paint[i]);
        }
    }

    /// <summary>Starts over from seed ground (new seeds), unpainted.</summary>
    /// <param name="seeds">The column's ground heights.</param>
    public void Reset(float[] seeds)
    {
        _seeds = seeds;
        _density = null;
        _paint = null;
    }

    /// <summary>The seed ground's density at a sample, as stored.</summary>
    /// <param name="i">The sample's index.</param>
    public short SeedStep(int i)
    {
        int x = i % RockGrid.Samples;
        int y = i / RockGrid.Samples % RockGrid.Samples;
        int z = i / (RockGrid.Samples * RockGrid.Samples);
        return Encode(_seeds[x + RockGrid.Samples * z] - (_baseY + y) * RockGrid.Step);
    }

    /// <summary>A density as a chunk keeps it: held to ±4 m, in 1/256 m, halves to even.</summary>
    /// <param name="v">The density, in metres.</param>
    public static short Encode(float v) => (short)Math.Round(RockMath.Clamp(v, -RockGrid.Band, RockGrid.Band) * 256.0);

    /// <summary>A density rounded as a chunk keeps it, in metres.</summary>
    /// <param name="v">The density, in metres.</param>
    public static float Quantize(float v) => Encode(v) / 256f;

    /// <summary>
    /// A density rounded as the oldest checkpoints' seed ground was: held to ±128 m instead of the band, in 1/256 m.
    /// </summary>
    /// <param name="v">The density, in metres.</param>
    public static float QuantizeWide(float v) => (short)Math.Round(RockMath.Clamp(v, -127.99609f, 127.99609f) * 256.0) / 256f;
}

/// <summary>Tunnelheim's paints for rock samples.</summary>
public static class RockPaint
{
    /// <summary>Untouched: dirt where the rock is dug, the ground's own look elsewhere.</summary>
    public const byte None = 0;

    /// <summary>Dirt.</summary>
    public const byte Dirt = 1;

    /// <summary>Cultivated.</summary>
    public const byte Cultivated = 2;

    /// <summary>Paved.</summary>
    public const byte Paved = 3;

    /// <summary>Painted back to the ground's own look.</summary>
    public const byte Natural = 4;

    /// <summary>Cleared of vegetation.</summary>
    public const byte NoVegetation = 5;
}

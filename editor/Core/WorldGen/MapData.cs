using TerrainEditor.Editing;

namespace ValheimGen;

/// <summary>
/// The inputs of Valheim's world map shader (Minimap.GenerateWorldMap): a biome colour texture, a
/// mask texture (r forest, g mist, b ashlands lava/ocean) and a height texture. The overview uses the
/// game's own layout (2048 texels of 12 m); detail windows use 1 m texels and include terrain edits.
/// </summary>
public sealed partial class MapData
{
    /// <summary>
    /// Texels per side of the overview, as the game's own map.
    /// </summary>
    public const int GlobalSize = 2048;

    /// <summary>
    /// Metres per overview texel of the game's own world.
    /// </summary>
    public const float GlobalPixel = 12f;

    /// <summary>
    /// Metres per overview texel of this world: the game's 12 m, grown with the world's edge (Expand World Size sizes
    /// the game's map the same way), so the 2048 texels cover the whole world.
    /// </summary>
    public float TexelMeters => GlobalPixel * Generator.Rules.TotalRadius / 10500f;

    /// <summary>
    /// The height the map draws the sea below: the world's real water level (Expand World Rivers' when it moves it),
    /// half a metre under it as the game's map draws its own (29.5 m for its 30).
    /// </summary>
    public float SeaLine => Generator.Rules.WaterLevel - 0.5f;

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
    /// Stops the overview's build (the map data is dropped for another world's).
    /// </summary>
    private readonly CancellationToken _cancel;

    /// <summary>
    /// How many detail windows are kept: the one shown and the one before it (panning back). A 2048-texel window is
    /// about 59 MB.
    /// </summary>
    private const int KeptDetails = 2;

    /// <summary>
    /// The last detail windows built, oldest first, all of the newest edit version; guarded by
    /// <see cref="_detailsLock"/>.
    /// </summary>
    private readonly List<((int X0, int Z0, int Size, int EditVersion) Key, Layers Layers)> _details = [];

    /// <summary>
    /// Guards <see cref="_details"/>.
    /// </summary>
    private readonly Lock _detailsLock = new();

    /// <summary>
    /// Runs when a detail window is not cached, before it is built; tests pause a build there.
    /// </summary>
    internal Action BeforeDetailBuild { get; set; } = () => { };

    /// <summary>
    /// Map data for the world whose generator <paramref name="terrain"/> uses; <paramref name="cancel"/> stops
    /// the overview's build (<see cref="Global"/> then throws <see cref="OperationCanceledException"/>).
    /// </summary>
    public MapData(TerrainService terrain, EditStore edits, CancellationToken cancel = default)
    {
        _terrain = terrain;
        _edits = edits;
        _cancel = cancel;
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
    /// The last overview built, by its generator's identity: the same world opened again (Reload, back to the
    /// worlds and in again) takes it as it is instead of building it again (seconds), while it is still in memory.
    /// Held weakly: once no map uses it, its ~58 MB go when memory is collected (the start page, another world).
    /// </summary>
    private static ((int Seed, int Version, bool Menu, string Rules) Key, WeakReference<Layers> Layers)? _lastGlobal;

    /// <summary>
    /// Guards <see cref="_lastGlobal"/>.
    /// </summary>
    private static readonly Lock LastGlobalLock = new();

    /// <summary>
    /// The overview: the last one built if it is this world's, else built from the generator.
    /// </summary>
    private Layers BuildGlobal()
    {
        // Cancelled before it starts: thrown as a build would, kept overview or not.
        _cancel.ThrowIfCancellationRequested();
        var key = Generator.Identity;
        lock (LastGlobalLock)
        {
            if (_lastGlobal is { } last && last.Key == key && last.Layers.TryGetTarget(out Layers? kept))
            {
                return kept;
            }
        }
        var layers = BuildGlobalRows();
        lock (LastGlobalLock)
        {
            _lastGlobal = (key, new WeakReference<Layers>(layers));
        }
        return layers;
    }

    /// <summary>
    /// Builds the overview from the generator alone, rows in parallel.
    /// </summary>
    private Layers BuildGlobalRows()
    {
        int n = GlobalSize;
        Layers layers = new(n, n);
        int half = n / 2;
        float pix = TexelMeters;
        // Same sample positions as Minimap.GenerateWorldMap; row i is world z. All the cores but one: unbounded,
        // the loop took every thread of the pool for as long as it ran (seconds), so nothing else that needs
        // one ran meanwhile (an HTTP answer from the game, a timer).
        Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1), CancellationToken = _cancel }, i =>
        {
            float wy = (i - half) * pix + pix / 2f;
            for (int j = 0; j < n; j++)
            {
                float wx = (j - half) * pix + pix / 2f;
                Heightmap.Biome biome = Generator.GetBiome(wx, wy);
                float h = Generator.GetBiomeHeight(biome, wx, wy, out _);
                layers.Set(i * n + j, MapHeight(h, biome), Pixel(biome, wx, wy), MaskColor(wx, wy, h, biome), null);
            }
        });
        return layers;
    }
}

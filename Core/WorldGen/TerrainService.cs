using System.Collections.Concurrent;
using TerrainEditor.Save;

namespace ValheimGen;

/// <summary>
/// Base (unmodified) terrain for a world, computed offline with the ported world generator.
/// </summary>
public sealed partial class TerrainService
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
        World world = new() { Seed = save.Seed, SeedName = save.SeedName, WorldGenVersion = save.WorldGenVersion };
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
}

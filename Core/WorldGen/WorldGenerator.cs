using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

/// <summary>
/// A Valheim world made from its seed as the game makes it: seas and continents, biomes, the ground's
/// height and colour masks, lakes, rivers and streams. Written for the editor, following the game's
/// formulas step for step, so a seed gives the same terrain to the bit (the reference tests compare it
/// with values recorded in the game), but for a few heights in the Deep North, less than a millimetre off
/// (floating point done in another order than the game's runtime).
/// </summary>
/// <remarks>
/// <para>
/// The formulas mix single and double precision; where a value is rounded back to float matters to the
/// last bit, so each rounding (a cast to float) is kept where the game has it.
/// </para>
/// <para>
/// The parts: this file (making a world, heights and slopes asked from outside), WorldGenerator.Biomes
/// (which biome a point is in), WorldGenerator.Ground (each biome's shape) and WorldGenerator.Water
/// (lakes, rivers and streams).
/// </para>
/// </remarks>
public sealed partial class WorldGenerator
{
    /// <summary>
    /// Draws the seed's offsets and lays out the water (not for the menu world), leaving this thread's Random
    /// state as it was.
    /// </summary>
    private WorldGenerator(World world)
    {
        _world = world;
        if (world.m_worldGenVersion <= 0)
        {
            _mountainFreeRadius = 1500f;
        }
        if (world.m_worldGenVersion <= 1)
        {
            _mistMinNoise = 0.5f;
            _swampMaxDistance = 8000f;
        }
        // The seed's draws, in the game's order (the rivers' and streams' own seeds among them).
        var saved = Rnd.state;
        try
        {
            Rnd.InitState(world.m_seed);
            _offsetA = Rnd.Range(-10000, 10000);
            _offsetB = Rnd.Range(-10000, 10000);
            _offsetForest = Rnd.Range(-10000, 10000);
            _offsetDetail = Rnd.Range(-10000, 10000);
            _riverSeed = Rnd.Range(int.MinValue, int.MaxValue);
            _streamSeed = Rnd.Range(int.MinValue, int.MaxValue);
            _offsetMist = Rnd.Range(-10000, 10000);
            if (!world.m_menu)
            {
                LayOutWater();
            }
        }
        finally
        {
            Rnd.state = saved;
        }
    }

    /// <summary>
    /// A generator for a world. Each caller keeps its own (the game has one, static): the editor can have
    /// several worlds at once.
    /// </summary>
    public static WorldGenerator Create(World world) => new(world);

    /// <summary>
    /// The Ashlands' noise settings: Euclidean cellular distance, two octaves.
    /// </summary>
    private static FastNoise MakeCells()
    {
        var n = new FastNoise(0);
        n.SetCellularDistanceFunction(FastNoise.CellularDistanceFunction.Euclidean);
        n.SetCellularReturnType(FastNoise.CellularReturnType.Distance);
        n.SetFractalOctaves(2);
        return n;
    }

    /// <summary>
    /// The world's seed.
    /// </summary>
    public int GetSeed() => _world.m_seed;

    // ---- Heights.
    /// <summary>
    /// The generated ground's height (metres) at a world position's x and z.
    /// </summary>
    public float GetHeight(Vector3 p) => GetHeight(p.x, p.z);

    /// <summary>
    /// The generated ground's height (metres) at a map point (y is world z).
    /// </summary>
    public float GetHeight(Vector2 p) => GetHeight(p.x, p.y);

    /// <summary>
    /// The generated ground's height (metres) at (x, z), in the biome there.
    /// </summary>
    public float GetHeight(float x, float z) => GetBiomeHeight(GetBiome(x, z), x, z, out _);

    /// <summary>
    /// The same, with the ground's mask (see <see cref="GetBiomeHeight"/>).
    /// </summary>
    public float GetHeight(float x, float z, out Color mask) => GetBiomeHeight(GetBiome(x, z), x, z, out mask);

    /// <summary>
    /// A shape in metres (the + 0.0 turns a negative zero to a positive one, as the game's sum does).
    /// </summary>
    private static float Scaled(float shape, float scale) => (float)((double)shape * scale + 0.0);

    /// <summary>
    /// How much of a forest the game would grow here (lower: denser; under 1.15 counts as forest).
    /// </summary>
    public static float GetForestFactor(Vector3 pos) => GenMath.Layers(pos * 0.01f * 0.4f, 3, 1.6f, 0.7f);

    /// <summary>
    /// Whether the game counts a point as forest (<see cref="GetForestFactor"/> under 1.15).
    /// </summary>
    public static bool InForest(Vector3 pos) => GetForestFactor(pos) < 1.15f;

    /// <summary>
    /// The world's radius (metres); past it, the sea deepens to the edge at 10500 m.
    /// </summary>
    public const float WorldSize = 10000f;

    /// <summary>
    /// The edge of the world (metres from the middle): past it the ground is a flat -400 m.
    /// </summary>
    private const float EdgeOfWorld = 10500f;

    /// <summary>
    /// Metres a unit of the generator's heights.
    /// </summary>
    private const float HeightScale = 200f;

    /// <summary>
    /// The world being made: seed, generator version, menu flag.
    /// </summary>
    private readonly World _world;

    /// <summary>
    /// Older worlds (the game's generator versions 0 and 1) keep their settings: the radius around the
    /// middle kept free of mountains, the mists' noise threshold and the swamps' greatest distance (metres).
    /// </summary>
    private readonly float _mountainFreeRadius = 1000f, _mistMinNoise = 0.4f, _swampMaxDistance = 6000f;

    /// <summary>
    /// The Ashlands' cellular and simplex noise (one for all worlds: it only holds settings).
    /// </summary>
    private static readonly FastNoise Cells = MakeCells();

    /// <summary>
    /// What the generator's ground depends on: two generators with the same make the same world.
    /// </summary>
    internal (int Seed, int Version, bool Menu) Identity => (_world.m_seed, _world.m_worldGenVersion, _world.m_menu);

    /// <summary>
    /// Where the noise fields sit for this seed (drawn from it): the continents' (A, B, which also place
    /// swamps and plains), the forests', the ground's small detail and the mists'.
    /// </summary>
    private readonly float _offsetA, _offsetB, _offsetForest, _offsetDetail, _offsetMist;
}

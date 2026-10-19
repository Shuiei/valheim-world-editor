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
/// (which biome a point is in), WorldGenerator.Ground (each biome's shape), WorldGenerator.Water
/// (lakes, rivers and streams), WorldGenerator.Rules (what a world generation mod changed: the
/// world's <see cref="World.Rules"/>, the game's own values for a world without one) and
/// WorldGenerator.BiomeRules (Expand World Data's world rules, when the world has them).
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
        Rules = world.Rules;
        if (world.WorldGenVersion <= 0)
        {
            _mountainFreeRadius = 1500f;
        }
        if (world.WorldGenVersion <= 1)
        {
            _mistMinNoise = 0.5f;
            _swampMaxDistance = 8000f;
        }
        // Expand World Size scales the swamps' reach with the radius (its WorldInfo.Patch), in float.
        _swampMaxDistance = _swampMaxDistance * Rules.Radius / 10000f / Rules.Stretch;
        // The seed's draws, in the game's order (the rivers' and streams' own seeds among them).
        var saved = Rnd.State;
        try
        {
            Rnd.InitState(world.Seed);
            _offsetA = Rnd.Range(-10000, 10000);
            _offsetB = Rnd.Range(-10000, 10000);
            _offsetForest = Rnd.Range(-10000, 10000);
            _offsetDetail = Rnd.Range(-10000, 10000);
            _riverSeed = Rnd.Range(int.MinValue, int.MaxValue);
            _streamSeed = Rnd.Range(int.MinValue, int.MaxValue);
            _offsetMist = Rnd.Range(-10000, 10000);
            if (Rules.DataInstalled)
            {
                // Expand World Data's four draws before the water is laid out (its Pregenerate prefix).
                _offsetAsh = Rnd.Range(-10000, 10000);
                _offsetNorth = Rnd.Range(-10000, 10000);
                _offsetMountain = Rnd.Range(-10000, 10000);
                _offsetOcean = Rnd.Range(-10000, 10000);
            }
            // The rules' values, and the seed's draws they replace (Expand World Size and Rivers).
            _v = new RuleValues(Rules, _offsetA, _offsetB, _offsetDetail, _riverSeed, _streamSeed);
            _riverSeed = _v.RiverSeed;
            _streamSeed = _v.StreamSeed;
            if (!world.Menu)
            {
                LayOutWater();
            }
        }
        finally
        {
            Rnd.State = saved;
        }
    }

    /// <summary>
    /// A generator for a world. Each caller keeps its own (the game has one, static): the editor can have
    /// several worlds at once.
    /// </summary>
    internal static WorldGenerator Create(World world) => new(world);

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
    public int GetSeed() => _world.Seed;

    // ---- Heights.
    /// <summary>
    /// The generated ground's height (metres) at a world position's x and z.
    /// </summary>
    internal float GetHeight(Vector3 p) => GetHeight(p.X, p.Z);

    /// <summary>
    /// The generated ground's height (metres) at a map point (y is world z).
    /// </summary>
    internal float GetHeight(Vector2 p) => GetHeight(p.X, p.Y);

    /// <summary>
    /// The generated ground's height (metres) at (x, z), in the biome there.
    /// </summary>
    public float GetHeight(float x, float z) => GetBiomeHeight(GetBiome(x, z), x, z, out _);

    /// <summary>
    /// The same, with the ground's mask (see <see cref="GetBiomeHeight"/>).
    /// </summary>
    internal float GetHeight(float x, float z, out Color mask) => GetBiomeHeight(GetBiome(x, z), x, z, out mask);

    /// <summary>
    /// A shape in metres (the + 0.0 turns a negative zero to a positive one, as the game's sum does).
    /// </summary>
    private static float Scaled(float shape, float scale) => (float)((double)shape * scale + 0.0);

    /// <summary>
    /// Metres a unit of the generator's heights.
    /// </summary>
    private const float HeightScale = 200f;

    /// <summary>
    /// The water level the game's zone system has as a literal (its vegetation's and locations' altitude and
    /// snapping, the biome point map's "above the sea", the map's mask): not a setting. Expand World Rivers moves
    /// the water (<see cref="WorldRules.WaterLevel"/>) and the generator's own thresholds, not these.
    /// </summary>
    public const float LiteralWaterLevel = 30f;

    /// <summary>
    /// The world being made: seed, generator version, menu flag, rules.
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
    internal (int Seed, int Version, bool Menu, string Rules) Identity => (_world.Seed, _world.WorldGenVersion, _world.Menu, Rules.Stamp);

    /// <summary>
    /// Where the noise fields sit for this seed (drawn from it): the continents' (A, B, which also place
    /// swamps and plains), the forests', the ground's small detail and the mists'.
    /// </summary>
    private readonly float _offsetA, _offsetB, _offsetForest, _offsetDetail, _offsetMist;
}

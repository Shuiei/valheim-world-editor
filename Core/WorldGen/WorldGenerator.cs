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
/// (lakes, rivers and streams) and WorldGenerator.Rules (what a world generation mod changed: the
/// world's <see cref="World.Rules"/>, the game's own values for a world without one).
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
        _v = new RuleValues(Rules);
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
            // Expand World Size and Rivers replace some of the seed's draws with settings.
            _continentA = Rules.OffsetX ?? _offsetA;
            _continentB = Rules.OffsetY ?? _offsetB;
            _detailX = Rules.HeightSeed ?? _offsetDetail;
            _riverSeed = Rules.RiverSeed ?? _riverSeed;
            _streamSeed = Rules.StreamSeed ?? _streamSeed;
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
    /// How much of a forest the game would grow here (lower: denser; under 1.15 counts as forest). Given the
    /// point as the game's callers give it: its zone system asks with the world position as it is, its map with
    /// the point stretched (<see cref="Stretched"/>).
    /// </summary>
    internal float GetForestFactor(Vector3 pos)
    {
        float f = GenMath.Layers(pos * 0.01f * 0.4f, 3, 1.6f, 0.7f);
        // Expand World Size's forest multiplier (its postfix, applied only when set).
        return _v.ForestOn ? f / _v.ForestMultiplier : f;
    }

    /// <summary>
    /// Whether the game counts a point as forest (<see cref="GetForestFactor"/> under 1.15).
    /// </summary>
    internal bool InForest(Vector3 pos) => GetForestFactor(pos) < 1.15f;

    /// <summary>
    /// The world's radius in the generator's coordinates (metres; the game's 10000, or the rules' radius over
    /// their stretch): past it, the sea deepens to the edge of the world.
    /// </summary>
    public float WorldSize => _v.WorldSize;

    /// <summary>
    /// A world coordinate as the generator's formulas take it: divided by the rules' stretch (Expand World
    /// Size's prefix on the game's GetBiome, GetBiomeHeight and the map's mask colour). The same coordinate
    /// with the game's own rules.
    /// </summary>
    internal float Stretched(float v) => v / _v.Stretch;

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

    /// <summary>
    /// The continents' offsets as the continents' height reads them: the seed's, or the rules' (Expand World Size's
    /// Offset X and Y replace them there, and only there: the swamps' and plains' fields keep the seed's).
    /// </summary>
    private readonly float _continentA, _continentB;

    /// <summary>
    /// The detail offset as the biomes' heights read it on their x coordinate: the seed's, or the rules' height seed
    /// (which replaces the first use in each of the game's seven biome heights, the x coordinate's; z keeps the
    /// seed's).
    /// </summary>
    private readonly float _detailX;
}

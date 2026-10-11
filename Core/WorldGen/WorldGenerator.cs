using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

// A Valheim world made from its seed as the game makes it: seas and continents, biomes, the ground's
// height and colour masks, lakes, rivers and streams. Written for the editor, following the game's
// formulas step for step, so a seed gives the same terrain to the bit (the reference tests compare it
// with values recorded in the game).
//
// The formulas mix single and double precision; where a value is rounded back to float matters to the
// last bit, so each rounding (a cast to float) is kept where the game has it.
//
// The parts: this file (making a world, heights and slopes asked from outside), WorldGenerator.Biomes
// (which biome a point is in), WorldGenerator.Ground (each biome's shape) and WorldGenerator.Water
// (lakes, rivers and streams).
public sealed partial class WorldGenerator
{
    // The world's radius (metres); past it, the sea deepens to the edge at 10500 m.
    public const float WorldSize = 10000f;

    private const float EdgeOfWorld = 10500f;

    // Metres a unit of the generator's heights.
    private const float HeightScale = 200f;

    private readonly World _world;

    // Where the noise fields sit for this seed (drawn from it): the continents' (A, B, which also place
    // swamps and plains), the forests', the ground's small detail and the mists'.
    private readonly float _offsetA, _offsetB, _offsetForest, _offsetDetail, _offsetMist;

    // Older worlds (the game's generator versions 0 and 1) keep their settings.
    private readonly float _mountainFreeRadius = 1000f, _mistMinNoise = 0.4f, _swampMaxDistance = 6000f;

    // The Ashlands' cellular and simplex noise (one for all worlds: it only holds settings).
    private static readonly FastNoise Cells = MakeCells();

    private static FastNoise MakeCells()
    {
        var n = new FastNoise(0);
        n.SetNoiseType(FastNoise.NoiseType.Cellular);
        n.SetCellularDistanceFunction(FastNoise.CellularDistanceFunction.Euclidean);
        n.SetCellularReturnType(FastNoise.CellularReturnType.Distance);
        n.SetFractalOctaves(2);
        return n;
    }

    // Each caller keeps its own (the game has one, static): the editor can have several worlds at once.
    public static WorldGenerator Create(World world) => new(world);

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
        Rnd.state = saved;
    }

    public int GetSeed() => _world.m_seed;

    // ---- Heights.

    public float GetHeight(Vector3 p) => GetHeight(p.x, p.z);

    public float GetHeight(Vector2 p) => GetHeight(p.x, p.y);

    public float GetHeight(float x, float z) => GetBiomeHeight(GetBiome(x, z), x, z, out _);

    public float GetHeight(float x, float z, out Color mask) => GetBiomeHeight(GetBiome(x, z), x, z, out mask);

    // The height the game uses while it lays out its streams (before the Ashlands' and Deep North's
    // own shapes; riverPreGen: the Deep North as it is for rivers).
    public float GetPregenerationHeight(float x, float z, bool riverPreGen) =>
        GetBiomeHeight(GetBiome(x, z), x, z, out _, preGeneration: true, riverPreGen);

    // The ground's height (metres) in a given biome, and its mask (the ground's colour channels, as
    // the game's heightmap keeps them).
    public float GetBiomeHeight(Heightmap.Biome biome, float x, float z, out Color mask, bool preGeneration = false, bool riverPreDN = true)
    {
        // The ground flattens towards the sea along the Ashlands' and Deep North's edges.
        float scale = preGeneration ? HeightScale : (float)(HeightScale * AshlandsGap(x, z) * CreateDeepNorthGap(x, z));
        mask = Color.black;
        if (_world.m_menu)
        {
            float shape = biome == Heightmap.Biome.Mountain ? MountainShape(x, z, menu: true) : MenuShape(x, z);
            return Scaled(shape, scale);
        }
        if (GenMath.Length(x, z) > EdgeOfWorld)
        {
            return -2f * HeightScale;
        }
        return biome switch
        {
            Heightmap.Biome.Meadows => Scaled(MeadowsShape(x, z), scale),
            Heightmap.Biome.BlackForest => Scaled(ForestShape(x, z), scale),
            Heightmap.Biome.Swamp => Scaled(SwampShape(x, z), scale),
            Heightmap.Biome.Mountain => Scaled(MountainShape(x, z, menu: false), scale),
            Heightmap.Biome.Plains => Scaled(PlainsShape(x, z), scale),
            Heightmap.Biome.Mistlands => Scaled(preGeneration ? ForestShape(x, z) : MistlandsShape(x, z, out mask), scale),
            Heightmap.Biome.AshLands => Scaled(preGeneration ? AshlandsRoughShape(x, z) : GetAshlandsHeight(x, z, out mask), scale),
            Heightmap.Biome.DeepNorth => Scaled(preGeneration ? DeepNorthRoughShape(x, z, riverPreDN) : DeepNorthShape(x, z, out mask), scale),
            Heightmap.Biome.Ocean => Scaled(Continent(x, z), scale),
            _ => 0f,
        };
    }

    // A shape in metres (the + 0.0 turns a negative zero to a positive one, as the game's sum does).
    private static float Scaled(float shape, float scale) => (float)((double)shape * scale + 0.0);

    // ---- The ground around a point.

    // The rise across a circle (the highest minus the lowest of ten random points in it) and the way
    // down, as the game samples it (from Unity's random sequence).
    public void GetTerrainDelta(Vector3 center, float radius, out float delta, out Vector3 slopeDirection)
    {
        float highest = -999999f, lowest = 999999f;
        Vector3 high = center, low = center;
        for (int i = 0; i < 10; i++)
        {
            var offset = Rnd.insideUnitCircle * radius;
            var p = center + new Vector3(offset.x, 0f, offset.y);
            float h = GetHeight(p.x, p.z);
            if (h < lowest)
            {
                lowest = h;
                low = p;
            }
            if (h > highest)
            {
                highest = h;
                high = p;
            }
        }
        delta = (float)((double)highest - lowest);
        slopeDirection = Vector3.Normalize(low - high);
    }

    // The ground's normal from its heights a metre to each side (radius is not used, as in the game).
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The game's signature; it ignores radius too.")]
    public Vector3 GetNormal(Vector2 point, float radius = 1f)
    {
        float east = GetHeight(point + Vector2.right), west = GetHeight(point + Vector2.left);
        float north = GetHeight(point + Vector2.up), south = GetHeight(point + Vector2.down);
        return new Vector3(2f * (east - west), 4f, 2f * (south - north)).normalized;
    }

    public Vector3 GetNormal(float x, float z, float radius = 1f) => GetNormal(new Vector2(x, z), radius);

    // Normals averaged over growing rings (up to 16), one every metersPerSample of radius.
    public Vector3 GetAverageNormal(Vector2 point, float radius, float metersPerSample = 1.5f)
    {
        if (radius <= 1.5f)
        {
            return GetNormal(point, radius);
        }
        int rings = Mathf.Min(Mathf.FloorToInt(radius / metersPerSample), 16);
        var sum = Vector3.zero;
        for (int i = 1; i <= rings; i++)
        {
            sum += GetNormal(point, radius * i) / rings;
        }
        return sum.normalized;
    }

    // How much of a forest the game would grow here (lower: denser; under 1.15 counts as forest).
    public static float GetForestFactor(Vector3 pos) => GenMath.Layers(pos * 0.01f * 0.4f, 3, 1.6f, 0.7f);

    public static bool InForest(Vector3 pos) => GetForestFactor(pos) < 1.15f;
}

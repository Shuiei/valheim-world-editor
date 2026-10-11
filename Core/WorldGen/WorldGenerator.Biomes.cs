using System.Collections.Concurrent;

namespace ValheimGen;

// Which biome a point is in: the sea where the continents dip, the Ashlands and the Deep North past
// their curved edges (south and north), mountains where the continents rise, then swamps, mists,
// plains and forests where their noise fields are high enough, at their distances from the middle,
// and the Meadows elsewhere.
public sealed partial class WorldGenerator
{
    // The Ashlands' and the Deep North's edges: circles 12 km round a point 4 km south (Ashlands) or
    // north (Deep North) of the middle, waved by the world's angle.
    private const float EdgeRadius = 12000f, AshlandsShift = -4000f, DeepNorthShift = 4000f;

    // Zones' biomes and edges as asked (by zone corner; the game keeps them the same way).
    private readonly ConcurrentDictionary<Vector2s, Heightmap.Biome> _cornerBiomes = new();
    private readonly ConcurrentDictionary<Vector2s, Heightmap.BiomeArea> _cornerAreas = new();

    public Heightmap.Biome GetBiome(Vector3 point) => GetBiome(point.x, point.z);

    // oceanLevel: the continents' height under which it is sea; waterAlwaysOcean: by the ground's
    // height instead (and before the Ashlands).
    public Heightmap.Biome GetBiome(float x, float z, float oceanLevel = 0.02f, bool waterAlwaysOcean = false)
    {
        if (_world.m_menu)
        {
            return MenuContinent(x, z) >= 0.4f ? Heightmap.Biome.Mountain : Heightmap.Biome.BlackForest;
        }
        float fromMiddle = GenMath.Length(x, z);
        float land = Continent(x, z);
        float wave = (float)((double)WorldAngle(x, z) * 100.0);
        if (waterAlwaysOcean && GetHeight(x, z) <= oceanLevel)
        {
            return Heightmap.Biome.Ocean;
        }
        if (IsAshlands(x, z))
        {
            return Heightmap.Biome.AshLands;
        }
        if (!waterAlwaysOcean && land <= oceanLevel)
        {
            return Heightmap.Biome.Ocean;
        }
        if (IsDeepnorth(x, z))
        {
            return Heightmap.Biome.DeepNorth;
        }
        if (land > 0.4f)
        {
            return Heightmap.Biome.Mountain;
        }
        if (Field(_offsetA, x, z) > 0.6f && fromMiddle > 2000f && fromMiddle < _swampMaxDistance && land > 0.05f && land < 0.25f)
        {
            return Heightmap.Biome.Swamp;
        }
        if (Field(_offsetMist, x, z) > _mistMinNoise && fromMiddle > (float)(6000.0 + wave) && fromMiddle < 10000f)
        {
            return Heightmap.Biome.Mistlands;
        }
        if (Field(_offsetB, x, z) > 0.4f && fromMiddle > (float)(3000.0 + wave) && fromMiddle < 8000f)
        {
            return Heightmap.Biome.Plains;
        }
        if (Field(_offsetForest, x, z) > 0.4f && fromMiddle > (float)(600.0 + wave) && fromMiddle < 6000f)
        {
            return Heightmap.Biome.BlackForest;
        }
        return fromMiddle > (float)(5000.0 + wave) ? Heightmap.Biome.BlackForest : Heightmap.Biome.Meadows;
    }

    // A biome's noise field: wide blobs, a kilometre or so across, placed by the seed.
    private static float Field(float offset, float x, float z) =>
        GenMath.Noise((double)(float)((double)offset + x) * (double)0.001f, (double)(float)((double)offset + z) * (double)0.001f);

    public static bool IsAshlands(float x, float z) =>
        (double)GenMath.Length(x, (float)((double)z + AshlandsShift)) > (double)EdgeRadius + (double)WorldAngle(x, z) * 100.0;

    public static bool IsDeepnorth(float x, float z) =>
        GenMath.Length(x, (float)((double)z + DeepNorthShift)) > (float)(EdgeRadius + (double)(float)((double)WorldAngle(x, z) * 100.0));

    // How far past the Ashlands' edge (in 300 m), for the sea's colour there.
    public static float GetAshlandsOceanGradient(float x, float z)
    {
        float shifted = z + AshlandsShift;
        return (float)(((double)GenMath.Length(x, shifted) - ((double)EdgeRadius + (double)WorldAngle(x, shifted) * 100.0)) / 300.0);
    }

    public static float GetAshlandsOceanGradient(Vector2 pos) => GetAshlandsOceanGradient(pos.x, pos.y);

    public static float GetAshlandsOceanGradient(Vector3 pos) => GetAshlandsOceanGradient(pos.x, pos.z);

    // 0 on the Ashlands' and the Deep North's edges, rising to 1 within 400 m: the ground sinks to the
    // sea along them.
    private static double AshlandsGap(float x, float z) => EdgeGap(x, z + AshlandsShift, x, z);

    public static double CreateDeepNorthGap(float x, float z) => EdgeGap(x, z + DeepNorthShift, x, z);

    private static double EdgeGap(float sx, float sz, float x, float z)
    {
        double past = (double)GenMath.Length(sx, sz) - ((double)EdgeRadius + (double)WorldAngle(x, z) * 100.0);
        return GenMath.SmoothBlend(0.0, 1.0, (float)GenMath.Clamp01(Math.Abs(past) / 400.0));
    }

    // 0 at the Deep North's edge to 1 200 m past it.
    public static double DeepNorthWaveFade(float x, float z) =>
        GenMath.Clamp01(((double)GenMath.Length(x, z + DeepNorthShift) - ((double)EdgeRadius + (double)WorldAngle(x, z) * 100.0)) / 200.0);

    // The wave of the Ashlands' and the Deep North's edges: twenty swings round the world, -1 to 1.
    // A point's height asks for it several times: the last one is kept on the thread (by the exact
    // bits of the point, so the answer is the one worked out again).
    public static float WorldAngle(float x, float z)
    {
        int bx = BitConverter.SingleToInt32Bits(x), bz = BitConverter.SingleToInt32Bits(z);
        if (t_angleKnown && t_angleX == bx && t_angleZ == bz)
        {
            return t_angle;
        }
        float angle = (float)Math.Sin((float)((double)(float)Math.Atan2(x, z) * 20.0));
        (t_angleX, t_angleZ, t_angle, t_angleKnown) = (bx, bz, angle, true);
        return angle;
    }

    [ThreadStatic]
    private static int t_angleX, t_angleZ;

    [ThreadStatic]
    private static float t_angle;

    [ThreadStatic]
    private static bool t_angleKnown;

    // A zone's area by its corner (zone x, z times 64): on a biome's edge when the corner and the
    // corners round it are not all in one biome, else in its middle.
    public Heightmap.BiomeArea GetBiomeArea(Vector2s corner)
    {
        if (_cornerAreas.TryGetValue(corner, out var area))
        {
            return area;
        }
        var biome = CornerBiome(corner);
        area = Heightmap.BiomeArea.Median;
        foreach (var (dx, dz) in AroundACorner)
        {
            if (CornerBiome(corner - new Vector2s(dx, dz)) != biome)
            {
                area = Heightmap.BiomeArea.Edge;
                break;
            }
        }
        _cornerAreas.TryAdd(corner, area);
        return area;
    }

    private static readonly (int, int)[] AroundACorner = { (-64, -64), (64, -64), (64, 64), (-64, 64), (-64, 0), (64, 0), (0, -64), (0, 64) };

    private Heightmap.Biome CornerBiome(Vector2s corner)
    {
        if (!_cornerBiomes.TryGetValue(corner, out var biome))
        {
            biome = GetBiome(corner.x, corner.y);
            _cornerBiomes.TryAdd(corner, biome);
        }
        return biome;
    }

    // A point's area as the game finds it from its biome sectors (AltBiomeWorldData). The editor has
    // none, and the game without them sees one sector everywhere: never an edge.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Asked of a world, as the game does.")]
    public Heightmap.BiomeArea GetBiomeArea(Vector3 point) => Heightmap.BiomeArea.Median;
}

using System.Collections.Concurrent;

namespace ValheimGen;

public sealed partial class WorldGenerator
{
    /// <summary>
    /// How far past the Ashlands' edge (in 300 m), for the sea's colour there; negative short of the edge.
    /// </summary>
    public static float GetAshlandsOceanGradient(float x, float z)
    {
        float shifted = z + AshlandsShift;
        return (float)(((double)GenMath.Length(x, shifted) - (EdgeRadius + (double)WorldAngle(x, shifted) * 100.0)) / 300.0);
    }

    /// <summary>
    /// The same at a map point (y is world z).
    /// </summary>
    public static float GetAshlandsOceanGradient(Vector2 pos) => GetAshlandsOceanGradient(pos.x, pos.y);

    /// <summary>
    /// The same at a world position's x and z.
    /// </summary>
    public static float GetAshlandsOceanGradient(Vector3 pos) => GetAshlandsOceanGradient(pos.x, pos.z);

    /// <summary>
    /// The wave of the Ashlands' and the Deep North's edges: twenty swings round the world, -1 to 1.
    /// A point's height asks for it several times: the last one is kept on the thread (by the exact
    /// bits of the point, so the answer is the one worked out again).
    /// </summary>
    public static float WorldAngle(float x, float z)
    {
        int bx = BitConverter.SingleToInt32Bits(x), bz = BitConverter.SingleToInt32Bits(z);
        if (_angleKnown && _angleX == bx && _angleZ == bz)
        {
            return _angle;
        }
        float angle = (float)Math.Sin((float)((double)(float)Math.Atan2(x, z) * 20.0));
        (_angleX, _angleZ, _angle, _angleKnown) = (bx, bz, angle, true);
        return angle;
    }

    /// <summary>
    /// WorldAngle's last answer on this thread.
    /// </summary>
    [ThreadStatic]
    private static float _angle;

    /// <summary>
    /// Whether this thread has a last answer yet.
    /// </summary>
    [ThreadStatic]
    private static bool _angleKnown;

    /// <summary>
    /// The bits of the point WorldAngle last worked out on this thread.
    /// </summary>
    [ThreadStatic]
    private static int _angleX, _angleZ;

    /// <summary>
    /// A zone's area by its corner (zone x, z times 64): on a biome's edge when the corner and the
    /// corners round it are not all in one biome, else in its middle.
    /// </summary>
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

    /// <summary>
    /// The eight neighbouring corners' offsets, metres.
    /// </summary>
    private static readonly (int, int)[] AroundACorner = [(-64, -64), (64, -64), (64, 64), (-64, 64), (-64, 0), (64, 0), (0, -64), (0, 64)];

    /// <summary>
    /// The biome at a zone corner, cached.
    /// </summary>
    private Heightmap.Biome CornerBiome(Vector2s corner)
    {
        if (!_cornerBiomes.TryGetValue(corner, out var biome))
        {
            biome = GetBiome(corner.x, corner.y);
            _cornerBiomes.TryAdd(corner, biome);
        }
        return biome;
    }

    /// <summary>
    /// Zones' biomes as asked (by zone corner; the game keeps them the same way).
    /// </summary>
    private readonly ConcurrentDictionary<Vector2s, Heightmap.Biome> _cornerBiomes = new();

    /// <summary>
    /// Zones' edges as asked (by zone corner), for <see cref="GetBiomeArea(Vector2s)"/>.
    /// </summary>
    private readonly ConcurrentDictionary<Vector2s, Heightmap.BiomeArea> _cornerAreas = new();

    /// <summary>
    /// 0 on the Ashlands' edge, rising to 1 within 400 m: the ground sinks to the sea along it.
    /// </summary>
    private static double AshlandsGap(float x, float z) => EdgeGap(x, z + AshlandsShift, x, z);

    /// <summary>
    /// The same for the Deep North's edge.
    /// </summary>
    public static double CreateDeepNorthGap(float x, float z) => EdgeGap(x, z + DeepNorthShift, x, z);

    /// <summary>
    /// 0 on an edge circle, rising (eased) to 1 within 400 m to either side.
    /// </summary>
    /// <param name="sx">x from the circle's middle.</param>
    /// <param name="sz">z from the circle's middle.</param>
    /// <param name="x">World x, for the edge's wave.</param>
    /// <param name="z">World z, for the edge's wave.</param>
    private static double EdgeGap(float sx, float sz, float x, float z)
    {
        double past = (double)GenMath.Length(sx, sz) - (EdgeRadius + (double)WorldAngle(x, z) * 100.0);
        return GenMath.SmoothBlend(0.0, 1.0, (float)GenMath.Clamp01(Math.Abs(past) / 400.0));
    }

    /// <summary>
    /// 0 at the Deep North's edge to 1 200 m past it.
    /// </summary>
    public static double DeepNorthWaveFade(float x, float z) =>
        GenMath.Clamp01(((double)GenMath.Length(x, z + DeepNorthShift) - (EdgeRadius + (double)WorldAngle(x, z) * 100.0)) / 200.0);
}

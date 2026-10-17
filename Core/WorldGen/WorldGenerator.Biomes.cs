
namespace ValheimGen;

// Which biome a point is in: the sea where the continents dip, the Ashlands and the Deep North past
// their curved edges (south and north), mountains where the continents rise, then swamps, mists,
// plains and forests where their noise fields are high enough, at their distances from the middle,
// and the Meadows elsewhere.
public sealed partial class WorldGenerator
{
    /// <summary>
    /// The biome at a world position's x and z.
    /// </summary>
    public Heightmap.Biome GetBiome(Vector3 point) => GetBiome(point.x, point.z);

    /// <summary>
    /// The biome at (x, z).
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="oceanLevel">The continents' height under which it is sea.</param>
    /// <param name="waterAlwaysOcean">Sea by the ground's height instead (and before the Ashlands).</param>
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
        return LandBiome(x, z, fromMiddle, land, wave);
    }

    /// <summary>
    /// A biome's noise field: wide blobs, a kilometre or so across, placed by the seed.
    /// </summary>
    private static float Field(float offset, float x, float z) =>
        GenMath.Noise((double)(float)((double)offset + x) * (double)0.001f, (double)(float)((double)offset + z) * (double)0.001f);

    /// <summary>
    /// Whether (x, z) is past the Ashlands' edge (south).
    /// </summary>
    public static bool IsAshlands(float x, float z) =>
        (double)GenMath.Length(x, (float)((double)z + AshlandsShift)) > EdgeRadius + (double)WorldAngle(x, z) * 100.0;

    /// <summary>
    /// Whether (x, z) is past the Deep North's edge (north).
    /// </summary>
    public static bool IsDeepnorth(float x, float z) =>
        GenMath.Length(x, (float)((double)z + DeepNorthShift)) > (float)(EdgeRadius + (double)(float)((double)WorldAngle(x, z) * 100.0));

    /// <summary>
    /// The Ashlands' and the Deep North's edges: circles 12 km round a point 4 km south (Ashlands) or
    /// north (Deep North) of the middle, waved by the world's angle.
    /// </summary>
    private const float EdgeRadius = 12000f, AshlandsShift = -4000f, DeepNorthShift = 4000f;
}

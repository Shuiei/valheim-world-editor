
namespace ValheimGen;

// Which biome a point is in: the sea where the continents dip, the Ashlands and the Deep North past
// their curved edges (south and north), mountains where the continents rise, then swamps, mists,
// plains and forests where their noise fields are high enough, at their distances from the middle,
// and the Meadows elsewhere. With Expand World Data's world rules, the rules instead
// (WorldGenerator.BiomeRules).
public sealed partial class WorldGenerator
{
    /// <summary>
    /// The biome at a world position's x and z.
    /// </summary>
    internal Heightmap.Biome GetBiome(Vector3 point) => GetBiome(point.X, point.Z);

    /// <summary>
    /// The biome at (x, z).
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="oceanLevel">The continents' height under which it is sea.</param>
    /// <param name="waterAlwaysOcean">Sea by the ground's height instead (and before the Ashlands).</param>
    public Heightmap.Biome GetBiome(float x, float z, float oceanLevel = 0.02f, bool waterAlwaysOcean = false)
    {
        if (_world.Menu)
        {
            return MenuContinent(x, z) >= 0.4f ? Heightmap.Biome.Mountain : Heightmap.Biome.BlackForest;
        }
        // Expand World Size: the world stretched (its prefix on the game's GetBiome divides the point).
        x = Stretched(x);
        z = Stretched(z);
        if (_v.World != null)
        {
            // Expand World Data's prefix: the rules in place of the game's layout.
            return RulesBiome(x, z, oceanLevel, waterAlwaysOcean);
        }
        float fromMiddle = GenMath.Length(x, z);
        float land = Continent(x, z);
        float wave = (float)((double)Wave(x, z) * 100.0);
        // The game's GetHeight, given the point as it is here: it stretches it again on its own way in.
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
    /// A biome's noise field: wide blobs, a kilometre or so across, placed by the seed (the rules' biome stretch
    /// spreads them: Expand World Size divides the point by it, in float, before the seed's offset is added; the offset
    /// is not divided).
    /// </summary>
    private float Field(float offset, float x, float z) =>
        GenMath.Noise((double)(float)((double)offset + (x / _v.BiomeStretchF)) * (double)0.001f, (double)(float)((double)offset + (z / _v.BiomeStretchF)) * (double)0.001f);

    /// <summary>
    /// Whether (x, z) is past the Ashlands' edge (south); with Expand World Data's world rules, whether the sea boils
    /// there (the rule the point passes boils). In the generator's coordinates (<see cref="Stretched"/>).
    /// </summary>
    public bool IsAshlands(float x, float z) =>
        _v.World != null ? Boiling(x, z) > 0f
        : (double)GenMath.Length(x, (float)((double)z + (double)_v.AshlandsYOffset)) > (double)_v.AshlandsMinDistance + (double)Wave(x, z) * 100.0;

    /// <summary>
    /// Whether (x, z) is past the Deep North's edge (north). In the generator's coordinates, but for the rivers'
    /// drawing, which asks with world metres as the game does.
    /// </summary>
    public bool IsDeepnorth(float x, float z) =>
        GenMath.Length(x, (float)((double)z + _v.DeepNorthShift)) > (float)(_v.DeepNorthRadius + (double)(float)((double)Wave(x, z) * 100.0));
}

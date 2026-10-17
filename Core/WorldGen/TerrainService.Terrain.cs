using System.Collections.Concurrent;

namespace ValheimGen;

public sealed partial class TerrainService
{
    /// <summary>
    /// VertexBiomes' biomes by zone, built on first use.
    /// </summary>
    private readonly ConcurrentDictionary<(int, int), int[]> _biomes = new();

    /// <summary>
    /// Biome at every vertex of a zone (65 x 65, row = z), for brush masks.
    /// </summary>
    public int[] VertexBiomes(int zx, int zz) => _biomes.GetOrAdd((zx, zz), k =>
    {
        int[] b = new int[65 * 65];
        float x0 = k.Item1 * 64f - 32f, z0 = k.Item2 * 64f - 32f;
        for (int i = 0; i < 65; i++)
        {
            for (int j = 0; j < 65; j++)
            {
                b[i * 65 + j] = (int)Generator.GetBiome(x0 + j, z0 + i);
            }
        }
        return b;
    });

    /// <summary>
    /// The game's paint mask for a zone before the saved edits: the generated mask (biome-specific; Color.black
    /// elsewhere) with the runtime modifiers' cleared paint (dirt or paving around locations...). 65x65 RGBA floats,
    /// cached; callers must not change the array.
    /// </summary>
    public float[] BaseMask(int zx, int zz)
    {
        Modified ground = _ground;
        return ground.Masks.GetOrAdd((zx, zz), k =>
        {
            float[] m = new float[65 * 65 * 4];
            float[] h = Build(k.Item1, k.Item2, m);
            ground.Modifiers?.Apply(k.Item1, k.Item2, h, m);
            return m;
        });
    }

    /// <summary>
    /// Heightmap.m_cornerBiomes: the biome sector at each zone corner. Sectors sample GetBiome on the
    /// 12 m grid of AltBiomeWorldData (WorldSpaceToMapSpace / MapSpaceToWorldSpace).
    /// </summary>
    /// <returns>Four biomes: corners (-x, -z), (+x, -z), (-x, +z), (+x, +z); Ocean beyond the world's edge.</returns>
    public int[] CornerBiomes(int zx, int zz)
    {
        static float Snap(float v) => SeedLocations.BiomeMap.MapToWorld(SeedLocations.BiomeMap.WorldToMap(v));
        float x0 = zx * 64f - 32f, z0 = zz * 64f - 32f;
        return [.. new[] { (x0, z0), (x0 + 64f, z0), (x0, z0 + 64f), (x0 + 64f, z0 + 64f) }.Select(c => { float sx = Snap(c.Item1), sz = Snap(c.Item2); return sx * sx + sz * sz > MapRadius * MapRadius ? (int)Heightmap.Biome.Ocean : (int)Generator.GetBiome(sx, sz); })];
    }

    /// <summary>
    /// Generated terrain only, without location flattening (not cached).
    /// </summary>
    public float[] RawZone(int zx, int zz) => Build(zx, zz, null);

    /// <summary>
    /// The biome at a world point.
    /// </summary>
    public Heightmap.Biome BiomeAt(float x, float z) => Generator.GetBiome(x, z);
}

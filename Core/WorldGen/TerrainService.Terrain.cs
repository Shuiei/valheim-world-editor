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
    /// grid of AltBiomeWorldData (WorldSpaceToMapSpace / MapSpaceToWorldSpace; Expand World Size's for its worlds).
    /// </summary>
    /// <returns>Four biomes: corners (-x, -z), (+x, -z), (-x, +z), (+x, +z); Ocean beyond the world's edge.</returns>
    public int[] CornerBiomes(int zx, int zz)
    {
        var grid = BiomeGrid.Of(Generator.Rules);
        float Snap(float v) => grid.MapToWorld(grid.WorldToMap(v));
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

    /// <summary>
    /// The water's colours with the player at a world point, as Expand World Data gives them with its "Custom water
    /// color" on (WaterColor.StartTransition): the point's biome's (when the biome file keeps data of it), the
    /// territory's there over them; none with the switch off.
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    public WaterColours WaterAt(float x, float z)
    {
        if (!Rules.CustomWaterColor)
        {
            return WaterColours.None;
        }
        var biome = Rules.Biomes.Of(BiomeAt(x, z)) is { HasData: true } b ? b.Water : WaterColours.None;
        return biome.Under(TerritoryAt(x, z)?.Water);
    }

    /// <summary>
    /// The territory a player at a world point is in, as Expand World Data finds it for the player's water and status
    /// effects (BiomeCalculator.GetTerritory: the point as it is, where the map's colours stretch it as the biomes);
    /// null for none.
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    public Territory? TerritoryAt(float x, float z) => Generator.TerritoryAt(x, z);

    /// <summary>
    /// The status effects Expand World Data puts on a player at a world point (its StatusEffectManager): the
    /// territory's when it gives any, else the biome's when the biome file keeps data of it; none otherwise.
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    public IReadOnlyList<StatusEffect> StatusEffectsAt(float x, float z)
    {
        if (TerritoryAt(x, z) is { StatusEffects.Count: > 0 } territory)
        {
            return territory.StatusEffects;
        }
        return Rules.Biomes.Of(BiomeAt(x, z)) is { HasData: true } biome ? biome.StatusEffects : [];
    }
}

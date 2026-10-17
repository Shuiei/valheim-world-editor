using ValheimGen;
using NVector3 = System.Numerics.Vector3;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.App;

/// <summary>
/// The ground the editor has now and the zones read from it (biomes, paint mask, ocean depth) for placing vegetation.
/// </summary>
public static partial class Regrow
{
    /// <summary>
    /// The ground the editor has now: heights every metre, triangles split like the game's terrain
    /// mesh (from (x + 1, z) to (x, z + 1)). wx0, wz0 are the world position of the first height.
    /// </summary>
    private sealed class Ground(float[] heights, int w, int h, float wx0, float wz0)
    {
        /// <summary>Whether the world point is on the grid.</summary>
        public bool Inside(float x, float z) => x >= wx0 && z >= wz0 && x < wx0 + w - 1 && z < wz0 + h - 1;

        /// <summary>
        /// The ground height at a world point, and the normal of its triangle; points off the grid are clamped onto it.
        /// </summary>
        public float Height(float x, float z, out NVector3 normal)
        {
            float gx = Math.Clamp(x - wx0, 0f, w - 1.001f), gz = Math.Clamp(z - wz0, 0f, h - 1.001f);
            int ix = (int)gx, iz = (int)gz;
            float fx = gx - ix, fz = gz - iz;
            float h00 = heights[iz * w + ix], h10 = heights[iz * w + ix + 1], h01 = heights[(iz + 1) * w + ix], h11 = heights[(iz + 1) * w + ix + 1];
            if (fx + fz <= 1f)
            {
                normal = NVector3.Normalize(new NVector3(-(h10 - h00), 1f, -(h01 - h00)));
                return h00 + (h10 - h00) * fx + (h01 - h00) * fz;
            }
            normal = NVector3.Normalize(new NVector3(-(h11 - h01), 1f, -(h11 - h10)));
            return h11 + (h01 - h11) * (1f - fx) + (h10 - h11) * (1f - fz);
        }

        /// <summary>The ground height at a world point.</summary>
        public float Height(float x, float z) => Height(x, z, out _);
    }
    /// <summary>The zone holding a world point, made once per <paramref name="cache"/>.</summary>
    private static Zone ZoneAt(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> cache, float x, float z)
    {
        var (zx, zz) = Save.ChunkMath.SectorOf(x, z);
        if (cache.TryGetValue((zx, zz), out Zone? zone))
        {
            return zone;
        }
        float cx = zx * 64f, cz = zz * 64f;
        float Depth(float px, float pz) => MathF.Max(0f, TerrainService.WaterLevel - ground.Height(px, pz));
        zone = new Zone
        {
            Corners = terrain.CornerBiomes(zx, zz),
            Mask = terrain.BaseMask(zx, zz),
            // Heightmap corners 0: (0, width), 1: (width, width), 2: (width, 0), 3: (0, 0).
            OceanDepth = [Depth(cx - 32f, cz + 32f), Depth(cx + 32f, cz + 32f), Depth(cx + 32f, cz - 32f), Depth(cx - 32f, cz - 32f)],
            Cx = cx,
            Cz = cz,
        };
        cache[(zx, zz)] = zone;
        ZoneReads++;
        return zone;
    }

    /// <summary>Whether the ground's slope at the spot (its normal) is within the rule's range.</summary>
    private static bool PassesSlope(Rule veg, NVector3 normal)
    {
        float maxTiltCos = MathF.Cos(MathF.PI / 180f * veg.maxTilt), minTiltCos = MathF.Cos(MathF.PI / 180f * veg.minTilt);
        return !(normal.Y < maxTiltCos || normal.Y > minTiltCos);
    }

    /// <summary>
    /// Whether the height difference of the ground around the spot is within the rule's range: ten random samples
    /// within its terrain delta radius (none when the rule has no radius).
    /// </summary>
    private static bool PassesTerrainDelta(Rule veg, Ground ground, float px, float pz)
    {
        if (!(veg.terrainDeltaRadius > 0f))
        {
            return true;
        }
        float hi = -999999f, lo = 999999f;
        for (int k = 0; k < 10; k++)
        {
            var c = Rnd.insideUnitCircle;
            float gh = ground.Height(px + c.x * veg.terrainDeltaRadius, pz + c.y * veg.terrainDeltaRadius);
            lo = MathF.Min(lo, gh);
            hi = MathF.Max(hi, gh);
        }
        float delta = hi - lo;
        return !(delta > veg.maxTerrainDelta || delta < veg.minTerrainDelta);
    }
}

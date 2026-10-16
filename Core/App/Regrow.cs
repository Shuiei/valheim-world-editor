using System.Numerics;
using System.Text.Json;
using TerrainEditor.Editing;
using ValheimGen;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.App;

/// <summary>
/// "Regrow nature": the trees, rocks, bushes and pickables the game would put in a zone, by its own
/// vegetation rules (ZoneSystem.m_vegetation and the LocationLists, read from the player's Valheim:
/// GameData; the tests use tests/fixtures/vegetation.json) and the same random draws
/// (ZoneSystem.PlaceVegetation, Unity's Random seeded per zone and rule), on the ground as it is now. What
/// the editor cannot know is skipped: the physics checks against objects already standing (IsBlocked,
/// snapping onto rocks), clear areas around locations, and the Deep North's alt biomes. The page drops spots
/// taken by objects that are there and keeps those in the selection.
/// </summary>
public static class Regrow
{
#pragma warning disable IDE1006 // named as the JSON keys they are read from
    /// <summary>
    /// One vegetation rule (a ZoneVegetation of the game), with its fields' names. Booleans are 0 or 1;
    /// distances in metres, tilts in degrees.
    /// </summary>
    private sealed class Rule
    {
        /// <summary>The prefab placed; its hash also seeds the rule's random draws.</summary>
        public string prefab { get; set; } = "";
        /// <summary>The rule's own name in the game.</summary>
        public string name { get; set; } = "";
        /// <summary>0: the rule is off.</summary>
        public int enable { get; set; }
        /// <summary>Lowest group count per zone, when <see cref="max"/> is 1 or more.</summary>
        public float min { get; set; }
        /// <summary>Below 1: the chance of one group in the zone; else the highest group count.</summary>
        public float max { get; set; }
        /// <summary>1: try up to 50 times the count to place the groups.</summary>
        public int forcePlacement { get; set; }
        /// <summary>Smallest uniform scale.</summary>
        public float scaleMin { get; set; } = 1f;
        /// <summary>Largest uniform scale.</summary>
        public float scaleMax { get; set; } = 1f;
        /// <summary>Largest random tilt on x and z (degrees).</summary>
        public float randTilt { get; set; }
        /// <summary>Chance (0..1) the object is turned to the ground's slope instead.</summary>
        public float chanceToUseGroundTilt { get; set; }
        /// <summary>The biomes it grows in (biome bit flags).</summary>
        public int biome { get; set; }
        /// <summary>Where in a biome (flags): 1 edge, 2 middle (median); 7 by default, anywhere.</summary>
        public int biomeArea { get; set; } = 7;
        /// <summary>The game's physics check against objects standing there; not done here.</summary>
        public int blockCheck { get; set; }
        /// <summary>The game's snapping onto rocks; not done here.</summary>
        public int snapToStaticSolid { get; set; }
        /// <summary>Lowest ground height, metres above the water level.</summary>
        public float minAltitude { get; set; } = -1000f;
        /// <summary>Highest ground height, metres above the water level.</summary>
        public float maxAltitude { get; set; } = 1000f;
        /// <summary>
        /// Lowest vegetation mask (paint mask alpha); no check when it equals <see cref="maxVegetation"/>.
        /// </summary>
        public float minVegetation { get; set; }
        /// <summary>Highest vegetation mask.</summary>
        public float maxVegetation { get; set; }
        /// <summary>Least ocean depth (metres); no check when it equals <see cref="maxOceanDepth"/>.</summary>
        public float minOceanDepth { get; set; }
        /// <summary>Greatest ocean depth (metres).</summary>
        public float maxOceanDepth { get; set; }
        /// <summary>Lowest ground slope (degrees).</summary>
        public float minTilt { get; set; }
        /// <summary>Highest ground slope (degrees).</summary>
        public float maxTilt { get; set; } = 90f;
        /// <summary>Radius of the ten random height samples for the terrain delta check; 0: no check.</summary>
        public float terrainDeltaRadius { get; set; }
        /// <summary>Largest height difference (metres) within <see cref="terrainDeltaRadius"/>.</summary>
        public float maxTerrainDelta { get; set; } = 2f;
        /// <summary>Smallest height difference (metres) within <see cref="terrainDeltaRadius"/>.</summary>
        public float minTerrainDelta { get; set; }
        /// <summary>1: put at the water level instead of the ground.</summary>
        public int snapToWater { get; set; }
        /// <summary>Raised this far above the ground (or water).</summary>
        public float groundOffset { get; set; }
        /// <summary>Fewest objects in a group.</summary>
        public int groupSizeMin { get; set; } = 1;
        /// <summary>Most objects in a group.</summary>
        public int groupSizeMax { get; set; } = 1;
        /// <summary>How far a group's other objects go from its first.</summary>
        public float groupRadius { get; set; }
        /// <summary>Distance from the world's middle; 0: no limit.</summary>
        public float minDistanceFromCenter { get; set; }
        /// <summary>Distance from the world's middle; 0: no limit.</summary>
        public float maxDistanceFromCenter { get; set; }
        /// <summary>1: only where the forest factor is between the thresholds below.</summary>
        public int inForest { get; set; }
        /// <summary>Lowest forest factor, with <see cref="inForest"/>.</summary>
        public float forestTresholdMin { get; set; }
        /// <summary>Highest forest factor, with <see cref="inForest"/>.</summary>
        public float forestTresholdMax { get; set; } = 1f;
        /// <summary>
        /// Random draws the object's components make when the game creates it (Awake): they move the
        /// sequence on for the rest of the rule.
        /// </summary>
        public int draws { get; set; }
    }
#pragma warning restore IDE1006

    /// <summary>The vegetation rules, in the game's order. None until Use: no world opens before then (GameData).</summary>
    private static volatile List<Rule> _rules = [];

    /// <summary>The rules as GameCatalogues.VegetationJson writes them.</summary>
    public static void Use(string json) => _rules = JsonSerializer.Deserialize<List<Rule>>(json)!;

    /// <summary>How many vegetation rules are loaded (0 before <see cref="Use"/>).</summary>
    public static int RuleCount => _rules.Count;

    /// <summary>
    /// Zones read (biomes, paint mask, ocean depths) on this thread so far: a test seam, each zone is read once per
    /// <see cref="Zones"/> call.
    /// </summary>
    [field: ThreadStatic]
    internal static int ZoneReads { get; private set; }

    /// <summary>One object the game would place: prefab, Unity world position, Euler rotation (degrees), scale.</summary>
    /// <param name="Name">The prefab.</param>
    /// <param name="X">World x (metres).</param>
    /// <param name="Y">Height (metres).</param>
    /// <param name="Z">World z (metres).</param>
    /// <param name="Rx">Rotation about x (degrees).</param>
    /// <param name="Ry">Rotation about y (degrees).</param>
    /// <param name="Rz">Rotation about z (degrees).</param>
    /// <param name="Scale">Uniform scale.</param>
    public sealed record Spot(string Name, float X, float Y, float Z, float Rx, float Ry, float Rz, float Scale);

    /// <summary>
    /// What the game would place in zones x0..x1, z0..z1 on the current ground. Kinds the editor cannot
    /// create (no networked object) are left out by the caller.
    /// </summary>
    /// <param name="terrain">The world's terrain (biomes, paint mask).</param>
    /// <param name="edits">The session's edits, for the ground as it is now.</param>
    /// <param name="seed">The world's seed.</param>
    /// <param name="x0">First zone column.</param>
    /// <param name="z0">First zone row.</param>
    /// <param name="x1">Last zone column (inclusive).</param>
    /// <param name="z1">Last zone row (inclusive).</param>
    public static List<Spot> Zones(TerrainService terrain, EditStore edits, int seed, int x0, int z0, int x1, int z1)
    {
        // The ground one zone around too: groups reach up to 20 m past their zone.
        var (w, h, heights) = HeightGrid.Read(terrain, edits, x0 - 1, z0 - 1, x1 + 1, z1 + 1);
        Ground ground = new(heights, w, h, (x0 - 1) * 64f - 32f, (z0 - 1) * 64f - 32f);
        List<Spot> spots = [];
        // Each zone's biomes, paint mask and ocean depths, made once for the whole call: groups reach into neighbours.
        Dictionary<(int, int), Zone> zones = [];
        // Unity's Random has one state per thread: put back what this thread had.
        Rnd.State saved = Rnd.state;
        try
        {
            for (int zz = z0; zz <= z1; zz++)
            {
                for (int zx = x0; zx <= x1; zx++)
                {
                    PlaceZone(terrain, ground, zones, seed, zx, zz, spots);
                }
            }
        }
        finally
        {
            Rnd.state = saved;
        }
        return spots;
    }

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

    /// <summary>
    /// One zone's heightmap as the game sees it while placing: corner biomes, edge or middle of a biome,
    /// the vegetation mask and the ocean depth at its corners.
    /// </summary>
    private sealed class Zone
    {
        /// <summary>The biome at each corner, as <c>TerrainService.CornerBiomes</c> gives them.</summary>
        public required int[] Corners;
        /// <summary>The base paint mask: 65 × 65 vertices, four floats (RGBA) each.</summary>
        public required float[] Mask;
        /// <summary>Ocean depth (metres below water) at the heightmap's four corners, in the game's corner order.</summary>
        public required float[] OceanDepth;
        /// <summary>The zone's middle in world metres.</summary>
        public float Cx, Cz;

        /// <summary>Whether any corner has one of these biomes (bit flags).</summary>
        public bool HaveBiome(int biome) => Corners.Any(c => (c & biome) != 0);

        /// <summary>The game's biome area: 2 (median) when all four corners are one biome, else 1 (edge).</summary>
        public int Area => Corners[0] == Corners[1] && Corners[0] == Corners[2] && Corners[0] == Corners[3] ? 2 : 1;   // Median : Edge

        /// <summary>The biome at a world point, weighting the corners by distance as the game does.</summary>
        public int Biome(float x, float z)
        {
            if (Corners[0] == Corners[1] && Corners[0] == Corners[2] && Corners[0] == Corners[3])
            {
                return Corners[0];
            }
            float nx = (x - Cx) / 64f + 0.5f, ny = (z - Cz) / 64f + 0.5f;
            Dictionary<int, float> weights = [];
            void Add(int b, float cx, float cy)
            {
                float dx = nx - cx, dy = ny - cy, d = MathF.Sqrt(dx * dx + dy * dy), k = 1.414f - d;
                weights[b] = weights.GetValueOrDefault(b) + k * k * k;
            }
            Add(Corners[0], 0f, 0f); Add(Corners[1], 1f, 0f); Add(Corners[2], 0f, 1f); Add(Corners[3], 1f, 1f);
            // Ties go to the lowest biome index, as the game walks its weights in index order.
            return weights.OrderByDescending(kv => kv.Value).ThenBy(kv => BitOperations.Log2((uint)kv.Key)).First().Key;
        }

        /// <summary>Heightmap.GetVegetationMask: the alpha of the base paint mask at the vertex under the point.</summary>
        public float VegetationMask(float x, float z)
        {
            int mx = Math.Clamp((int)MathF.Floor(x - 0.5f - Cx + 0.5f + 32f), 0, 64), my = Math.Clamp((int)MathF.Floor(z - 0.5f - Cz + 0.5f + 32f), 0, 64);
            return Mask[(my * 65 + mx) * 4 + 3];
        }

        /// <summary>The ocean depth at a world point, between the four corners' depths.</summary>
        public float Ocean(float x, float z)
        {
            int vx = (int)MathF.Floor(x - Cx + 0.5f) + 32, vy = (int)MathF.Floor(z - Cz + 0.5f) + 32;
            float t = vx / 64f, t2 = vy / 64f;
            float a = Lerp(OceanDepth[3], OceanDepth[2], t), b = Lerp(OceanDepth[0], OceanDepth[1], t);
            return Lerp(a, b, t2);
        }
    }

    /// <summary>Unity's Mathf.Lerp: t clamped to 0..1.</summary>
    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    /// <summary>The zone holding a world point, made once per <paramref name="cache"/>.</summary>
    private static Zone ZoneAt(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> cache, float x, float z)
    {
        int zx = (int)MathF.Floor((x + 32f) / 64f), zz = (int)MathF.Floor((z + 32f) / 64f);
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

    /// <summary>
    /// ZoneSystem.PlaceVegetation for one zone: every enabled rule of its biomes, the same draws in the same
    /// order, adding what passes the checks to <paramref name="spots"/>. <paramref name="zones"/> holds the zones
    /// read so far, shared by every zone of one <see cref="Zones"/> call.
    /// </summary>
    private static void PlaceZone(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> zones, int seed, int zx, int zz, List<Spot> spots)
    {
        Zone hmap = ZoneAt(terrain, ground, zones, zx * 64f, zz * 64f);
        float cx = zx * 64f, cz = zz * 64f;
        foreach (Rule veg in _rules)
        {
            if (veg.enable == 0 || !hmap.HaveBiome(veg.biome))
            {
                continue;
            }
            Rnd.InitState(seed + zx * 4271 + zz * 9187 + Save.StableHash.Of(veg.prefab));
            int count = 1;
            if (veg.max < 1f)
            {
                if (Rnd.value > veg.max)
                {
                    continue;
                }
            }
            else
            {
                count = Rnd.Range((int)veg.min, (int)veg.max + 1);
            }
            float maxTiltCos = MathF.Cos(MathF.PI / 180f * veg.maxTilt), minTiltCos = MathF.Cos(MathF.PI / 180f * veg.minTilt);
            float reach = 32f - veg.groupRadius;
            int tries = veg.forcePlacement != 0 ? count * 50 : count, placedGroups = 0;
            for (int i = 0; i < tries; i++)
            {
                float vx = Rnd.Range(cx - reach, cx + reach), vz = Rnd.Range(cz - reach, cz + reach);
                int groupSize = Rnd.Range(veg.groupSizeMin, veg.groupSizeMax + 1);
                bool placed = false;
                for (int j = 0; j < groupSize; j++)
                {
                    float px = vx, pz = vz;
                    if (j > 0)
                    {
                        float f = Rnd.value * MathF.PI * 2f, r = Rnd.Range(0f, veg.groupRadius);
                        px += MathF.Sin(f) * r;
                        pz += MathF.Cos(f) * r;
                    }
                    float ry = Rnd.Range(0, 360);
                    float scale = Rnd.Range(veg.scaleMin, veg.scaleMax);
                    float rx = Rnd.Range(-veg.randTilt, veg.randTilt), rz = Rnd.Range(-veg.randTilt, veg.randTilt);
                    if (!ground.Inside(px, pz))
                    {
                        continue;
                    }
                    float py = ground.Height(px, pz, out NVector3 normal);
                    Zone hm = ZoneAt(terrain, ground, zones, px, pz);
                    if ((veg.biome & hm.Biome(px, pz)) == 0 || (veg.biomeArea & hm.Area) == 0)
                    {
                        continue;
                    }
                    float altitude = py - TerrainService.WaterLevel;
                    if (altitude < veg.minAltitude || altitude > veg.maxAltitude)
                    {
                        continue;
                    }
                    if (!veg.minVegetation.Equals(veg.maxVegetation))
                    {
                        float m = hm.VegetationMask(px, pz);
                        if (m > veg.maxVegetation || m < veg.minVegetation)
                        {
                            continue;
                        }
                    }
                    if (!veg.minOceanDepth.Equals(veg.maxOceanDepth))
                    {
                        float d = hm.Ocean(px, pz);
                        if (d < veg.minOceanDepth || d > veg.maxOceanDepth)
                        {
                            continue;
                        }
                    }
                    if (normal.Y < maxTiltCos || normal.Y > minTiltCos)
                    {
                        continue;
                    }
                    if (veg.terrainDeltaRadius > 0f)
                    {
                        float hi = -999999f, lo = 999999f;
                        for (int k = 0; k < 10; k++)
                        {
                            var c = Rnd.insideUnitCircle;
                            float gh = ground.Height(px + c.x * veg.terrainDeltaRadius, pz + c.y * veg.terrainDeltaRadius);
                            lo = MathF.Min(lo, gh);
                            hi = MathF.Max(hi, gh);
                        }
                        float delta = hi - lo;
                        if (delta > veg.maxTerrainDelta || delta < veg.minTerrainDelta)
                        {
                            continue;
                        }
                    }
                    if (veg.minDistanceFromCenter > 0f || veg.maxDistanceFromCenter > 0f)
                    {
                        float dist = MathF.Sqrt(px * px + pz * pz);
                        if ((veg.minDistanceFromCenter > 0f && dist < veg.minDistanceFromCenter) || (veg.maxDistanceFromCenter > 0f && dist > veg.maxDistanceFromCenter))
                        {
                            continue;
                        }
                    }
                    if (veg.inForest != 0)
                    {
                        float forest = WorldGenerator.GetForestFactor(new ValheimGen.Vector3(px, py, pz));
                        if (forest < veg.forestTresholdMin || forest > veg.forestTresholdMax)
                        {
                            continue;
                        }
                    }
                    if (veg.snapToWater != 0)
                    {
                        py = TerrainService.WaterLevel;
                    }
                    py += veg.groundOffset;
                    NVector3 euler;
                    if (veg.chanceToUseGroundTilt > 0f && Rnd.value <= veg.chanceToUseGroundTilt)
                    {
                        // Quaternion.LookRotation(Cross(normal, Euler(0, y, 0) * forward), normal).
                        float a = ry * MathF.PI / 180f;
                        NVector3 fwd = new(MathF.Sin(a), 0f, MathF.Cos(a));
                        euler = BlueprintFormats.ToEuler(LookRotation(NVector3.Cross(normal, fwd), normal));
                    }
                    else
                    {
                        euler = new NVector3(rx, ry, rz);
                    }
                    spots.Add(new Spot(veg.prefab, px, py, pz, euler.X, euler.Y, euler.Z, scale));
                    for (int k = veg.draws; k > 0; k--)
                    {
                        Rnd.NextUInt();
                    }
                    placed = true;
                }
                if (placed)
                {
                    placedGroups++;
                }
                if (placedGroups >= count)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Unity's Quaternion.LookRotation(forward, up).</summary>
    private static NQuaternion LookRotation(NVector3 forward, NVector3 up)
    {
        var z = NVector3.Normalize(forward);
        var x = NVector3.Normalize(NVector3.Cross(up, z));
        var y = NVector3.Cross(z, x);
        // Columns x, y, z of the rotation matrix.
        float m00 = x.X, m01 = y.X, m02 = z.X, m10 = x.Y, m11 = y.Y, m12 = z.Y, m20 = x.Z, m21 = y.Z, m22 = z.Z;
        float trace = m00 + m11 + m22;
        if (trace > 0f)
        {
            float s = MathF.Sqrt(trace + 1f) * 2f;
            return new NQuaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
        }
        if (m00 > m11 && m00 > m22)
        {
            float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
            return new NQuaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
        }
        if (m11 > m22)
        {
            float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
            return new NQuaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
        }
        float s2 = MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
        return new NQuaternion((m02 + m20) / s2, (m12 + m21) / s2, 0.25f * s2, (m10 - m01) / s2);
    }
}

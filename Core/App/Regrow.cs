using System.Text.Json;
using TerrainEditor.Editing;
using ValheimGen;
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
public static partial class Regrow
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
        Rnd.RandomState saved = Rnd.State;
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
            Rnd.State = saved;
        }
        return spots;
    }
}

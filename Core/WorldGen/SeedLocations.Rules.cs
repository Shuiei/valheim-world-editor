using System.Text.Json;

namespace ValheimGen;

public static partial class SeedLocations
{
#pragma warning disable IDE1006 // named as the JSON keys they are read from and written as
    /// <summary>
    /// One location kind's rule (the game's ZoneSystem.ZoneLocation), as locations.json keeps it. Flags are 0 or 1.
    /// </summary>
    public sealed class Rule
    {
        /// <summary>
        /// The location's prefab name (the kind; also what seeds its random draws).
        /// </summary>
        public string prefab { get; set; } = "";
        /// <summary>
        /// The location's name, as alt biomes block it.
        /// </summary>
        public string name { get; set; } = "";
        /// <summary>
        /// Non-zero when the game lays it out.
        /// </summary>
        public int enable { get; set; }
        /// <summary>
        /// The biomes it goes in (Heightmap.Biome flags).
        /// </summary>
        public int biome { get; set; }
        /// <summary>
        /// Where in those biomes (Heightmap.BiomeArea flags: edge, middle).
        /// </summary>
        public int biomeArea { get; set; }
        /// <summary>
        /// How many the game tries to lay out.
        /// </summary>
        public int quantity { get; set; }
        /// <summary>
        /// Non-zero for the kinds laid out first (start, bosses, traders...), which also get more tries.
        /// </summary>
        public int prioritized { get; set; }
        /// <summary>
        /// Non-zero for kinds laid out from the middle out (the start temple).
        /// </summary>
        public int centerFirst { get; set; }
        /// <summary>
        /// Non-zero when one is all there may be.
        /// </summary>
        public int unique { get; set; }
        /// <summary>
        /// Its group, for minDistanceFromSimilar (empty: none).
        /// </summary>
        public string group { get; set; } = "";
        /// <summary>
        /// No instance of the same prefab or group within this many metres (0: no check).
        /// </summary>
        public float minDistanceFromSimilar { get; set; }
        /// <summary>
        /// Its "max" group, for maxDistanceFromSimilar (empty: none).
        /// </summary>
        public string groupMax { get; set; } = "";
        /// <summary>
        /// An instance of the same prefab or "max" group needed within this many metres (0: no check).
        /// </summary>
        public float maxDistanceFromSimilar { get; set; }
        /// <summary>
        /// Its inner radius, metres; the larger of it and exteriorRadius is kept from the zone's edges.
        /// </summary>
        public float interiorRadius { get; set; }
        /// <summary>
        /// Its outer radius, metres: the circle the slope is measured on (see also interiorRadius).
        /// </summary>
        public float exteriorRadius { get; set; }
        /// <summary>
        /// The least rise across its size, metres.
        /// </summary>
        public float minTerrainDelta { get; set; }
        /// <summary>
        /// The most rise across its size, metres.
        /// </summary>
        public float maxTerrainDelta { get; set; }
        /// <summary>
        /// The ground's vegetation (mask alpha) must be above this (0: no check).
        /// </summary>
        public float minimumVegetation { get; set; }
        /// <summary>
        /// The ground's vegetation must be below this (1: no check).
        /// </summary>
        public float maximumVegetation { get; set; }
        /// <summary>
        /// Non-zero for kinds that want the greenest spots (see StandsOut).
        /// </summary>
        public int surroundCheckVegetation { get; set; }
        /// <summary>
        /// How far the vegetation around the spot is read, metres.
        /// </summary>
        public float surroundCheckDistance { get; set; }
        /// <summary>
        /// On how many rings.
        /// </summary>
        public int surroundCheckLayers { get; set; }
        /// <summary>
        /// The share of the way from the average to the best a spot must reach.
        /// </summary>
        public float surroundBetterThanAverage { get; set; }
        /// <summary>
        /// Non-zero when the forest factor must lie between the two thresholds.
        /// </summary>
        public int inForest { get; set; }
        /// <summary>
        /// The least forest factor (lower: denser forest).
        /// </summary>
        public float forestTresholdMin { get; set; }
        /// <summary>
        /// The most forest factor.
        /// </summary>
        public float forestTresholdMax { get; set; }
        /// <summary>
        /// The least flat distance from the middle on the ground, metres (0: none).
        /// </summary>
        public float minDistanceFromCenter { get; set; }
        /// <summary>
        /// The most flat distance from the middle on the ground, metres (0: none).
        /// </summary>
        public float maxDistanceFromCenter { get; set; }
        /// <summary>
        /// The least distance from the middle, metres (0: none); where centerFirst kinds start.
        /// </summary>
        public float minDistance { get; set; }
        /// <summary>
        /// The most distance from the middle, metres (0: none).
        /// </summary>
        public float maxDistance { get; set; }
        /// <summary>
        /// The least height above the sea (30 m), metres; 0 or more draws only points above the sea.
        /// </summary>
        public float minAltitude { get; set; }
        /// <summary>
        /// The most height above the sea, metres.
        /// </summary>
        public float maxAltitude { get; set; }
        /// <summary>
        /// 1: turned along the slope it stands on when spawned (ZoneLocation.m_slopeRotation), before randomRotation
        /// is looked at.
        /// </summary>
        public int slopeRotation { get; set; }
        /// <summary>
        /// 1: turned by a random multiple of 22.5° when spawned (ZoneLocation.m_randomRotation), rolled from the
        /// zone's seed.
        /// </summary>
        public int randomRotation { get; set; }
        /// <summary>
        /// 1: stands at the water's level, not the ground's (ZoneLocation.m_snapToWater).
        /// </summary>
        public int snapToWater { get; set; }
        /// <summary>
        /// The alt biome that adds it; null for the game's own kinds.
        /// </summary>
        public string? altBiome { get; set; }
    }

    /// <summary>
    /// An alt biome (Dark Meadows, Lox Plains...) and the location kinds it keeps out of its areas.
    /// </summary>
    public sealed class AltBiomeRule
    {
        /// <summary>
        /// The alt biome's name.
        /// </summary>
        public string name { get; set; } = "";
        /// <summary>
        /// Names (Rule.name) of the location kinds it blocks.
        /// </summary>
        public List<string> blockLocationNames { get; set; } = [];
    }

    /// <summary>
    /// The game's rules: its locations in its order (ZoneSystem's list, the location lists, the alt
    /// biomes' additions) and the alt biomes.
    /// </summary>
    public sealed class RuleSet
    {
        /// <summary>
        /// The location kinds in the game's order.
        /// </summary>
        public List<Rule> locations { get; set; } = [];
        /// <summary>
        /// The alt biomes.
        /// </summary>
        public List<AltBiomeRule> altBiomes { get; set; } = [];

        /// <summary>
        /// As locations.json keeps them: one location or alt biome a line (nulls left out).
        /// </summary>
        public string ToJson() =>
            "{\"locations\": [\n" + string.Join(",\n", locations.Select(l => JsonSerializer.Serialize(l, JsonOptions))) +
            "\n],\n\"altBiomes\": [\n" + string.Join(",\n", altBiomes.Select(a => JsonSerializer.Serialize(a, JsonOptions))) + "\n]}\n";

        /// <summary>
        /// Reads locations.json; throws <see cref="JsonException"/> when it holds no rules.
        /// </summary>
        public static RuleSet FromJson(string json) => JsonSerializer.Deserialize<RuleSet>(json) ?? throw new JsonException("no rules");
    }
#pragma warning restore IDE1006
}

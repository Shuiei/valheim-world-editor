using System.Text.Json;
using System.Text.Json.Serialization;

namespace ValheimGen;

public static partial class SeedLocations
{
    /// <summary>
    /// One location kind's rule (the game's ZoneSystem.ZoneLocation), as locations.json keeps it. Flags are 0 or 1.
    /// </summary>
    public sealed class Rule
    {
        /// <summary>
        /// The location's prefab name (the kind; also what seeds its random draws).
        /// </summary>
        [JsonPropertyName("prefab")]
        public string Prefab { get; set; } = "";
        /// <summary>
        /// The location's name, as alt biomes block it.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        /// <summary>
        /// Non-zero when the game lays it out.
        /// </summary>
        [JsonPropertyName("enable")]
        public int Enable { get; set; }
        /// <summary>
        /// The biomes it goes in (Heightmap.Biome flags).
        /// </summary>
        [JsonPropertyName("biome")]
        public int Biome { get; set; }
        /// <summary>
        /// Where in those biomes (Heightmap.BiomeArea flags: edge, middle).
        /// </summary>
        [JsonPropertyName("biomeArea")]
        public int BiomeArea { get; set; }
        /// <summary>
        /// How many the game tries to lay out.
        /// </summary>
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
        /// <summary>
        /// Non-zero for the kinds laid out first (start, bosses, traders...), which also get more tries.
        /// </summary>
        [JsonPropertyName("prioritized")]
        public int Prioritized { get; set; }
        /// <summary>
        /// Non-zero for kinds laid out from the middle out (the start temple).
        /// </summary>
        [JsonPropertyName("centerFirst")]
        public int CenterFirst { get; set; }
        /// <summary>
        /// Non-zero when one is all there may be.
        /// </summary>
        [JsonPropertyName("unique")]
        public int Unique { get; set; }
        /// <summary>
        /// Its group, for minDistanceFromSimilar (empty: none).
        /// </summary>
        [JsonPropertyName("group")]
        public string Group { get; set; } = "";
        /// <summary>
        /// No instance of the same prefab or group within this many metres (0: no check).
        /// </summary>
        [JsonPropertyName("minDistanceFromSimilar")]
        public float MinDistanceFromSimilar { get; set; }
        /// <summary>
        /// Its "max" group, for maxDistanceFromSimilar (empty: none).
        /// </summary>
        [JsonPropertyName("groupMax")]
        public string GroupMax { get; set; } = "";
        /// <summary>
        /// An instance of the same prefab or "max" group needed within this many metres (0: no check).
        /// </summary>
        [JsonPropertyName("maxDistanceFromSimilar")]
        public float MaxDistanceFromSimilar { get; set; }
        /// <summary>
        /// Its inner radius, metres; the larger of it and exteriorRadius is kept from the zone's edges.
        /// </summary>
        [JsonPropertyName("interiorRadius")]
        public float InteriorRadius { get; set; }
        /// <summary>
        /// Its outer radius, metres: the circle the slope is measured on (see also interiorRadius).
        /// </summary>
        [JsonPropertyName("exteriorRadius")]
        public float ExteriorRadius { get; set; }
        /// <summary>
        /// The least rise across its size, metres.
        /// </summary>
        [JsonPropertyName("minTerrainDelta")]
        public float MinTerrainDelta { get; set; }
        /// <summary>
        /// The most rise across its size, metres.
        /// </summary>
        [JsonPropertyName("maxTerrainDelta")]
        public float MaxTerrainDelta { get; set; }
        /// <summary>
        /// The ground's vegetation (mask alpha) must be above this (0: no check).
        /// </summary>
        [JsonPropertyName("minimumVegetation")]
        public float MinimumVegetation { get; set; }
        /// <summary>
        /// The ground's vegetation must be below this (1: no check).
        /// </summary>
        [JsonPropertyName("maximumVegetation")]
        public float MaximumVegetation { get; set; }
        /// <summary>
        /// Non-zero for kinds that want the greenest spots (see StandsOut).
        /// </summary>
        [JsonPropertyName("surroundCheckVegetation")]
        public int SurroundCheckVegetation { get; set; }
        /// <summary>
        /// How far the vegetation around the spot is read, metres.
        /// </summary>
        [JsonPropertyName("surroundCheckDistance")]
        public float SurroundCheckDistance { get; set; }
        /// <summary>
        /// On how many rings.
        /// </summary>
        [JsonPropertyName("surroundCheckLayers")]
        public int SurroundCheckLayers { get; set; }
        /// <summary>
        /// The share of the way from the average to the best a spot must reach.
        /// </summary>
        [JsonPropertyName("surroundBetterThanAverage")]
        public float SurroundBetterThanAverage { get; set; }
        /// <summary>
        /// Non-zero when the forest factor must lie between the two thresholds.
        /// </summary>
        [JsonPropertyName("inForest")]
        public int InForest { get; set; }
        /// <summary>
        /// The least forest factor (lower: denser forest).
        /// </summary>
        [JsonPropertyName("forestTresholdMin")]
        public float ForestTresholdMin { get; set; }
        /// <summary>
        /// The most forest factor.
        /// </summary>
        [JsonPropertyName("forestTresholdMax")]
        public float ForestTresholdMax { get; set; }
        /// <summary>
        /// The least flat distance from the middle on the ground, metres (0: none).
        /// </summary>
        [JsonPropertyName("minDistanceFromCenter")]
        public float MinDistanceFromCenter { get; set; }
        /// <summary>
        /// The most flat distance from the middle on the ground, metres (0: none).
        /// </summary>
        [JsonPropertyName("maxDistanceFromCenter")]
        public float MaxDistanceFromCenter { get; set; }
        /// <summary>
        /// The least distance from the middle, metres (0: none); where centerFirst kinds start.
        /// </summary>
        [JsonPropertyName("minDistance")]
        public float MinDistance { get; set; }
        /// <summary>
        /// The most distance from the middle, metres (0: none).
        /// </summary>
        [JsonPropertyName("maxDistance")]
        public float MaxDistance { get; set; }
        /// <summary>
        /// The least height above the sea (30 m), metres; 0 or more draws only points above the sea.
        /// </summary>
        [JsonPropertyName("minAltitude")]
        public float MinAltitude { get; set; }
        /// <summary>
        /// The most height above the sea, metres.
        /// </summary>
        [JsonPropertyName("maxAltitude")]
        public float MaxAltitude { get; set; }
        /// <summary>
        /// 1: turned along the slope it stands on when spawned (ZoneLocation.m_slopeRotation), before randomRotation
        /// is looked at.
        /// </summary>
        [JsonPropertyName("slopeRotation")]
        public int SlopeRotation { get; set; }
        /// <summary>
        /// 1: turned by a random multiple of 22.5° when spawned (ZoneLocation.m_randomRotation), rolled from the
        /// zone's seed.
        /// </summary>
        [JsonPropertyName("randomRotation")]
        public int RandomRotation { get; set; }
        /// <summary>
        /// 1: stands at the water's level, not the ground's (ZoneLocation.m_snapToWater).
        /// </summary>
        [JsonPropertyName("snapToWater")]
        public int SnapToWater { get; set; }
        /// <summary>
        /// The alt biome that adds it; null for the game's own kinds.
        /// </summary>
        [JsonPropertyName("altBiome")]
        public string? AltBiome { get; set; }
    }

    /// <summary>
    /// An alt biome (Dark Meadows, Lox Plains...) and the location kinds it keeps out of its areas.
    /// </summary>
    public sealed class AltBiomeRule
    {
        /// <summary>
        /// The alt biome's name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        /// <summary>
        /// Names (Rule.name) of the location kinds it blocks.
        /// </summary>
        [JsonPropertyName("blockLocationNames")]
        public List<string> BlockLocationNames { get; set; } = [];
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
        [JsonPropertyName("locations")]
        public List<Rule> Locations { get; set; } = [];
        /// <summary>
        /// The alt biomes.
        /// </summary>
        [JsonPropertyName("altBiomes")]
        public List<AltBiomeRule> AltBiomes { get; set; } = [];

        /// <summary>
        /// As locations.json keeps them: one location or alt biome a line (nulls left out).
        /// </summary>
        public string ToJson() =>
            "{\"locations\": [\n" + string.Join(",\n", Locations.Select(l => JsonSerializer.Serialize(l, JsonOptions))) +
            "\n],\n\"altBiomes\": [\n" + string.Join(",\n", AltBiomes.Select(a => JsonSerializer.Serialize(a, JsonOptions))) + "\n]}\n";

        /// <summary>
        /// Reads locations.json; throws <see cref="JsonException"/> when it holds no rules.
        /// </summary>
        public static RuleSet FromJson(string json) => JsonSerializer.Deserialize<RuleSet>(json) ?? throw new JsonException("no rules");
    }
}

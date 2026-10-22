using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ValheimGen;

/// <summary>
/// One location kind's rule (the game's ZoneSystem.ZoneLocation), as locations.json keeps it. Flags are 0 or 1.
/// </summary>
public sealed class LocationRule
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
    /// 1: no vegetation grows within its exterior radius of it in its zone (the game's square round it, a circle
    /// with Expand World Data's vegetation file).
    /// </summary>
    [JsonPropertyName("clearArea")]
    public int ClearArea { get; set; }
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
    /// <summary>1: its icon shows on the map wherever it is laid out (ZoneLocation.m_iconAlways).</summary>
    [JsonPropertyName("iconAlways")]
    public int IconAlways { get; set; }
    /// <summary>1: its icon shows on the map once its zone is made (ZoneLocation.m_iconPlaced).</summary>
    [JsonPropertyName("iconPlaced")]
    public int IconPlaced { get; set; }
    /// <summary>
    /// The alt biome that adds it; null for the game's own kinds.
    /// </summary>
    [JsonPropertyName("altBiome")]
    public string? AltBiome { get; set; }

    // ---- Expand World Data's location file.
    /// <summary>
    /// The game's prefab a clone ("Name:Tag" in the mod's file) is made of; null when the prefab is the game's own
    /// (<see cref="Prefab"/>). The mod counts clones of one prefab as the same kind for the distance checks.
    /// </summary>
    [JsonPropertyName("basePrefab")]
    public string? BasePrefab { get; set; }
    /// <summary>
    /// The groups the kind belongs to for the mod's distance checks (its "groups", "group" and "groupMax"); null
    /// for the game's own kinds.
    /// </summary>
    [JsonPropertyName("groups")]
    public List<string>? Groups { get; set; }
    /// <summary>
    /// The mod's "awayFrom": the kind stays this far from any instance of these kinds or groups (then
    /// <see cref="Group"/> is a made-up name and <see cref="MinDistanceFromSimilar"/> 1, as the mod sets them); null
    /// for none.
    /// </summary>
    [JsonPropertyName("awayFrom")]
    public List<DistanceRule>? AwayFrom { get; set; }
    /// <summary>
    /// The mod's "closeTo": the kind needs an instance within reach for every row (any of a row's kinds or groups);
    /// null for none.
    /// </summary>
    [JsonPropertyName("closeTo")]
    public List<List<DistanceRule>>? CloseTo { get; set; }
    /// <summary>True for a location the mod builds from a blueprint file (no prefab: predicted, not drawn); null otherwise.</summary>
    [JsonPropertyName("blueprint")]
    public bool? Blueprint { get; set; }
    /// <summary>
    /// True when the mod changes what the location spawns (swapped or added objects, data, commands, a random seed,
    /// a dungeon, terrain changes): its contents cannot be known; null otherwise.
    /// </summary>
    [JsonPropertyName("custom")]
    public bool? Custom { get; set; }
    /// <summary>
    /// Expand World Data's "randomDamage" for the kind ("true", "all" or empty: the file's word, which replaces the
    /// prefab's own random damage); null for a game kind (its prefab's Location.m_applyRandomDamage).
    /// </summary>
    [JsonPropertyName("randomDamage")]
    public string? RandomDamage { get; set; }
    /// <summary>
    /// Expand World Data's "iconAlways" for the kind: "true" (its own name's icon), an icon's name (with its size and
    /// whether it moves after commas), or empty / "false" for none; null for a game kind (<see cref="IconAlways"/>).
    /// </summary>
    [JsonPropertyName("iconAlwaysName")]
    public string? IconAlwaysName { get; set; }
    /// <summary>Expand World Data's "iconPlaced" for the kind, as <see cref="IconAlwaysName"/>; null for a game kind.</summary>
    [JsonPropertyName("iconPlacedName")]
    public string? IconPlacedName { get; set; }
    /// <summary>
    /// What Expand World Data's location file changes about its contents, when it changes any (with <see cref="Custom"/>
    /// or <see cref="Blueprint"/>); null for none. Not kept in locations.json (the files are read again).
    /// </summary>
    [JsonIgnore]
    public LocationContents? Contents { get; set; }

    /// <summary>The game's prefab the kind is made of (<see cref="BasePrefab"/>, else <see cref="Prefab"/>).</summary>
    [JsonIgnore]
    public string Base => BasePrefab ?? Prefab;
}

/// <summary>One of the mod's distance rules: a kind or group, and how far (metres).</summary>
/// <param name="Name">The kind's prefab name or a group's name.</param>
/// <param name="Distance">The distance, metres.</param>
public sealed record DistanceRule([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("distance")] float Distance);

/// <summary>
/// The game's rules: its locations in its order (ZoneSystem's list, the location lists, the alt
/// biomes' additions) and the alt biomes.
/// </summary>
public sealed class LocationRuleSet
{
    /// <summary>How the rules are written: values left at null left out.</summary>
    internal static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>
    /// The location kinds in the game's order.
    /// </summary>
    [JsonPropertyName("locations")]
    public List<LocationRule> Locations { get; set; } = [];
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
    public static LocationRuleSet FromJson(string json) => JsonSerializer.Deserialize<LocationRuleSet>(json) ?? throw new JsonException("no rules");
}

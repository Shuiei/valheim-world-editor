using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ValheimGen;

#pragma warning disable IDE1006 // named as the JSON keys they are read from (the game's vegetation list, GameCatalogues.VegetationJson)
/// <summary>
/// One vegetation rule (a ZoneVegetation of the game): what the game places in a zone of its biomes and how, as
/// Regrow replays it. Read from the game's own list (the editor's vegetation.json), or made from Expand World Data's
/// vegetation file, whose rules may draw one of several prefabs, scale each axis apart, and measure their distance from
/// a centre of their own (the fields after <see cref="altBiome"/>). Booleans are 0 or 1; distances in metres, tilts in
/// degrees.
/// </summary>
public sealed class VegetationRule
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
    /// <summary>1: only spots whose surrounding vegetation (rings round them) is good enough against the zone's others.</summary>
    public int surroundCheckVegetation { get; set; }
    /// <summary>The outer ring's radius of the surround check, metres.</summary>
    public float surroundCheckDistance { get; set; } = 20f;
    /// <summary>How many rings the surround check reads.</summary>
    public int surroundCheckLayers { get; set; } = 2;
    /// <summary>The share of the way from the average sum to the best a spot must reach (0: the average).</summary>
    public float surroundBetterThanAverage { get; set; }
    /// <summary>
    /// The alt biome that adds the rule (it grows only in that alt biome's areas); null for a rule of every area of
    /// its biomes.
    /// </summary>
    public string? altBiome { get; set; }

    // ---- Expand World Data's vegetation file.
    /// <summary>
    /// The prefabs the rule draws one of for each object, when it names several (the mod's "prefab" list; one draw
    /// before the object is made); null for the one <see cref="prefab"/>.
    /// </summary>
    public string[]? prefabs { get; set; }
    /// <summary>
    /// The smallest scale on each axis (x, z, y as the mod writes them: the game's x, y, z here), when the mod scales
    /// an object again after the game's own draw: a range with different ends, drawn once more (one draw for a uniform
    /// range, three for an axis-wise one). Null: the game's uniform scale.
    /// </summary>
    public float[]? scaleMinAxes { get; set; }
    /// <summary>The largest scale on each axis, with <see cref="scaleMinAxes"/>.</summary>
    public float[]? scaleMaxAxes { get; set; }
    /// <summary>0: each axis drawn apart (three draws); 1: one draw for all three.</summary>
    public int scaleUniform { get; set; } = 1;
    /// <summary>
    /// The mod's own centre (x, then what it writes as the centre's y), when the rule has one: its distance check is
    /// then measured from it, in place of the game's from the middle. Null: none.
    /// </summary>
    public float[]? center { get; set; }
    /// <summary>The least and most distance from <see cref="center"/>, metres (most 0: no limit); null: no check.</summary>
    public float[]? distance { get; set; }
    /// <summary>1: each object keeps the ground within <see cref="clearRadius"/> of it clear of the vegetation placed after it in its zone.</summary>
    public int clearArea { get; set; }
    /// <summary>
    /// Metres: the radius an object clears (<see cref="clearArea"/>), and how much wider every clear area of its zone is
    /// for this rule.
    /// </summary>
    public float clearRadius { get; set; }
    /// <summary>
    /// The data each object is given (the file's "data", then its "drops"), as far as it can be written; null for none.
    /// Not kept in vegetation.json (the files are read again).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ContentField>? data { get; set; }
    /// <summary>
    /// When <see cref="prefab"/> is a blueprint of the mod's blueprint folder: its parts, and those of the blueprints it
    /// nests, by blueprint name; null for a prefab. Not kept in vegetation.json.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, IReadOnlyList<BlueprintPart>>? blueprints { get; set; }
}
#pragma warning restore IDE1006

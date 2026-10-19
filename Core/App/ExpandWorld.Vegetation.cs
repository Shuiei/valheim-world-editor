using ValheimGen;

namespace TerrainEditor.App;

// The vegetation rules from the vegetation files' entries (expand_vegetation.yaml).
public static partial class ExpandWorld
{
    /// <summary>
    /// The vegetation rules from the vegetation files' entries, in file order, as the mod's FromData makes them: a
    /// rule may name several prefabs (one drawn an object), scale by axis, measure its distance from a centre of its
    /// own, and be on or off by the world's global keys. The draws an object's components make come from the game's
    /// rule of the same prefab (none known for a prefab the game's list has not: noted). A rule of an alt biome is
    /// passed over (the editor does not lay alt biomes out).
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="game">The game's rules (for the components' draws).</param>
    /// <param name="biomes">The world's biomes (for the names).</param>
    /// <param name="radius">The world's radius, metres.</param>
    /// <param name="globalKeys">The world's global keys (a key's name, lower-cased).</param>
    /// <param name="notes">Where a rule passed over or a value not understood is noted.</param>
    internal static List<VegetationRule> VegetationOf(IReadOnlyList<Entry> entries, IReadOnlyList<VegetationRule> game, BiomeTable biomes, float radius, IReadOnlySet<string> globalKeys, List<string> notes)
    {
        List<VegetationRule> rules = [];
        var draws = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var g in game)
        {
            draws.TryAdd(g.prefab, g.draws);
        }
        HashSet<string> unknown = [];
        int altBiomes = 0;
        foreach (var entry in entries)
        {
            var f = new Fields(entry, "vegetation", notes);
            string[] prefabs = SplitList(f.Str("prefab"));
            if (prefabs.Length == 0)
            {
                continue;
            }
            if (f.Str("altBiome").Trim() != "")
            {
                altBiomes++;
                continue;
            }
            foreach (string p in prefabs.Where(p => !draws.ContainsKey(p)))
            {
                unknown.Add(p);
            }
            rules.Add(VegetationOf(f, prefabs, biomes, radius, globalKeys, p => draws.GetValueOrDefault(p), notes));
        }
        if (unknown.Count > 0)
        {
            notes.Add($"expand_vegetation: the game's list has no rule for {string.Join(", ", unknown.Order(StringComparer.Ordinal))}: their objects' own draws are taken as none");
        }
        if (altBiomes > 0)
        {
            notes.Add($"expand_vegetation: {altBiomes} rules of alt biomes are not regrown (the editor does not lay alt biomes out)");
        }
        return rules;
    }

    /// <summary>One rule from its entry.</summary>
    private static VegetationRule VegetationOf(Fields f, string[] prefabs, BiomeTable biomes, float radius, IReadOnlySet<string> globalKeys, Func<string, int> drawsOf, List<string> notes)
    {
        float[] scaleMin = ScaleOf(f.Str("scaleMin", "1")), scaleMax = ScaleOf(f.Str("scaleMax", "1"));
        (float[]? axesMin, float[]? axesMax) = scaleMin.SequenceEqual(scaleMax) ? (null, null) : (scaleMin, scaleMax);
        float minDistance = DistWhenPositive(f.F("minDistance", 0f), radius), maxDistance = DistWhenPositive(f.F("maxDistance", 0f), radius);
        (string[]? several, int[]? severalDraws) = prefabs.Length > 1 ? (prefabs, prefabs.Select(drawsOf).ToArray()) : (null, null);
        float centerX = f.F("centerX", 0f), centerY = f.F("centerY", 0f);
        bool ownCentre = centerX != 0f || centerY != 0f;
        bool enabled = f.B("enabled", true);
        if (enabled)
        {
            var required = f.Rows("requiredGlobalKey").Select(SplitList).Where(row => row.Length > 0).ToList();
            var forbidden = f.Rows("forbiddenGlobalKey").Select(SplitList).Where(row => row.Length > 0).ToList();
            bool Has(string key) => globalKeys.Contains(key.Trim().ToLowerInvariant());
            enabled = (required.Count == 0 || required.All(row => row.Any(Has))) && !(forbidden.Count > 0 && forbidden.All(row => row.Any(Has)));
        }
        return new VegetationRule
        {
            prefab = prefabs[0],
            name = f.Str("prefab"),
            prefabs = several,
            prefabDraws = severalDraws,
            draws = drawsOf(prefabs[0]),
            enable = enabled ? 1 : 0,
            min = f.F("min", 1f),
            max = f.F("max", 1f),
            forcePlacement = Flag(f, "forcePlacement"),
            scaleMin = scaleMin[0],
            scaleMax = scaleMax[0],
            scaleMinAxes = axesMin,
            scaleMaxAxes = axesMax,
            scaleUniform = Bit(f, "scaleUniform", true),
            randTilt = f.F("randTilt", 0f),
            chanceToUseGroundTilt = f.F("chanceToUseGroundTilt", 0f),
            biome = (int)biomes.Parse(f.Str("biome"), name => notes.Add($"expand_vegetation: {name} is no biome (left out)")),
            biomeArea = AreasOf(f, "biomeArea", notes),
            blockCheck = Bit(f, "blockCheck", true),
            snapToStaticSolid = Flag(f, "snapToStaticSolid"),
            minAltitude = f.F("minAltitude", 0f),
            maxAltitude = f.F("maxAltitude", 10000f),
            minVegetation = f.F("minVegetation", 0f),
            maxVegetation = f.F("maxVegetation", 0f),
            minOceanDepth = f.F("minOceanDepth", 0f),
            maxOceanDepth = f.F("maxOceanDepth", 0f),
            minTilt = f.F("minTilt", 0f),
            maxTilt = f.F("maxTilt", 90f),
            terrainDeltaRadius = f.F("terrainDeltaRadius", 0f),
            maxTerrainDelta = f.F("maxTerrainDelta", 10f),
            minTerrainDelta = f.F("minTerrainDelta", 0f),
            snapToWater = Flag(f, "snapToWater"),
            groundOffset = f.F("groundOffset", 0f),
            groupSizeMin = Int(f.Str("groupSizeMin")) ?? 1,
            groupSizeMax = Int(f.Str("groupSizeMax")) ?? 1,
            groupRadius = f.F("groupRadius", 0f),
            inForest = Flag(f, "inForest"),
            forestTresholdMin = f.F("forestTresholdMin", 0f),
            forestTresholdMax = f.F("forestTresholdMax", 1f),
            // The game's own check from the middle, unless the rule has a centre: then the mod's check instead.
            minDistanceFromCenter = ownCentre ? 0f : minDistance,
            maxDistanceFromCenter = ownCentre ? 0f : maxDistance,
            center = ownCentre ? [Dist(centerX, radius), Dist(centerY, radius)] : null,
            distance = ownCentre && (minDistance != 0f || maxDistance != 0f) ? [minDistance, maxDistance] : null,
        };
    }

    /// <summary>
    /// A scale as the mod parses one ("1", or "x, z, y"): the game's x, y, z; a missing or unreadable number 0, then 0
    /// on x is 1 and 0 on y or z is x.
    /// </summary>
    /// <param name="text">The scale's text.</param>
    internal static float[] ScaleOf(string text)
    {
        string[] parts = text.Split(',');
        float x = Channel(parts, 0, 0f), z = Channel(parts, 1, 0f), y = Channel(parts, 2, 0f);
        // Exactly 0 is the mod's sentinel for an axis not given.
        x = x == 0f ? 1f : x;
        return [x, y == 0f ? x : y, z == 0f ? x : z];
    }
}

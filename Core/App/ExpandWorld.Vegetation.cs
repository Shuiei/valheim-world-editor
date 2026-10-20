using ValheimGen;

namespace TerrainEditor.App;

// The vegetation rules from the vegetation files' entries (expand_vegetation.yaml).
public static partial class ExpandWorld
{
    /// <summary>
    /// The vegetation rules from the vegetation files' entries, in file order, as the mod's FromData makes them: a
    /// rule may name several prefabs (one drawn an object), scale by axis, measure its distance from a centre of its
    /// own, and be on or off by the world's global keys. The draws an object's components make come from the game's
    /// rule of the same prefab (none known for a prefab the game's list has not: noted). A rule of an alt biome grows
    /// in that alt biome's areas only. Its objects get the entry's data ("data", then "drops"); a rule whose prefab is a
    /// blueprint grows the blueprint's parts. What cannot be written ahead of the game is left out, noted.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="game">The game's rules (for the components' draws).</param>
    /// <param name="biomes">The world's biomes (for the names).</param>
    /// <param name="radius">The world's radius, metres.</param>
    /// <param name="globalKeys">The world's global keys (a key's name, lower-cased).</param>
    /// <param name="notes">Where a rule passed over or a value not understood is noted.</param>
    /// <param name="data">The world's data entries (what "data" names); none by default.</param>
    /// <param name="blueprints">The folder the mod reads blueprint files from; null for none.</param>
    internal static List<VegetationRule> VegetationOf(IReadOnlyList<Entry> entries, IReadOnlyList<VegetationRule> game, BiomeTable biomes, float radius, IReadOnlySet<string> globalKeys, List<string> notes, DataTable? data = null, string? blueprints = null)
    {
        List<VegetationRule> rules = [];
        var draws = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var g in game)
        {
            draws.TryAdd(g.prefab, g.draws);
        }
        HashSet<string> unknown = [];
        foreach (var entry in entries)
        {
            var f = new Fields(entry, "vegetation", notes);
            string[] prefabs = SplitList(f.Str("prefab"));
            if (prefabs.Length == 0)
            {
                continue;
            }
            var rule = VegetationOf(f, prefabs, biomes, radius, globalKeys, p => draws.GetValueOrDefault(p), notes);
            AddModContents(rule, f, data ?? DataTable.Empty, blueprints, notes);
            foreach (string p in prefabs.Where(p => !draws.ContainsKey(p) && rule.blueprints?.ContainsKey(p) != true))
            {
                unknown.Add(p);
            }
            rules.Add(rule);
        }
        if (unknown.Count > 0)
        {
            notes.Add($"expand_vegetation: the game's list has no rule for {string.Join(", ", unknown.Order(StringComparer.Ordinal))}: their objects' own draws are taken as none");
        }
        return rules;
    }

    /// <summary>
    /// What the mod gives a rule's objects (VegetationConvert): the entry's data, then its "drops" (the drop table the
    /// object's drops are taken from, by name's hash); and, when its one prefab is no prefab of the game, the blueprint of
    /// that name (the mod looks the prefab field up whole, so a list is never a blueprint). Data or parts that cannot be
    /// written ahead of the game are left out, noted.
    /// </summary>
    private static void AddModContents(VegetationRule rule, Fields f, DataTable data, string? blueprints, List<string> notes)
    {
        var why = new List<string>();
        var values = data.Resolve(f.Raw("data"));
        if (values.Why != null)
        {
            why.Add(values.Why);
        }
        List<ContentField> fields = [.. values.Fields];
        if (f.Str("drops") is { Length: > 0 } drops)
        {
            fields.Add(new ContentField("ints", DropsKey, Save.StableHash.Of(drops).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        rule.data = fields.Count > 0 ? fields : null;
        if (rule.prefabs == null && !Known(rule.prefab))
        {
            rule.blueprints = BlueprintsOf(blueprints, f.Str("prefab"), data, why, notes);
        }
        if (why.Count > 0)
        {
            notes.Add($"expand_vegetation: {f.Str("prefab")} grows without what cannot be written ahead of the game: {string.Join(", ", why.Distinct())}");
        }
    }

    /// <summary>The data key of the drop table Expand World Data gives an object ("ews_drops").</summary>
    private static readonly int DropsKey = Save.StableHash.Of("ews_drops");

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
            var required = KeyRows(f, "requiredGlobalKeys", "requiredGlobalKey");
            var forbidden = KeyRows(f, "forbiddenGlobalKeys", "forbiddenGlobalKey");
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
            altBiome = NonBlank(f.Str("altBiome")),
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
            groupSizeMin = f.I("groupSizeMin", 1),
            groupSizeMax = f.I("groupSizeMax", 1),
            groupRadius = f.F("groupRadius", 0f),
            inForest = Flag(f, "inForest"),
            forestTresholdMin = f.F("forestTresholdMin", 0f),
            forestTresholdMax = f.F("forestTresholdMax", 1f),
            // The game's own check from the middle, unless the rule has a centre: then the mod's check instead.
            minDistanceFromCenter = ownCentre ? 0f : minDistance,
            maxDistanceFromCenter = ownCentre ? 0f : maxDistance,
            center = ownCentre ? [Dist(centerX, radius), Dist(centerY, radius)] : null,
            distance = ownCentre && (minDistance != 0f || maxDistance != 0f) ? [minDistance, maxDistance] : null,
            surroundCheckVegetation = Flag(f, "surroundCheckVegetation"),
            surroundCheckDistance = f.F("surroundCheckDistance", 20f),
            surroundCheckLayers = f.I("surroundCheckLayers", 2),
            surroundBetterThanAverage = f.F("surroundBetterThanAverage", 0f),
            clearArea = Flag(f, "clearArea"),
            clearRadius = f.F("clearRadius", 0f),
        };
    }

    /// <summary>
    /// A rule's global key rows: the field's name since Data 1.79 (plural; the mod renames the old one in its files),
    /// else the older name a file written before it still has.
    /// </summary>
    private static List<string[]> KeyRows(Fields f, string key, string older) =>
        [.. f.Rows(f.Has(key) ? key : older).Select(SplitList).Where(row => row.Length > 0)];

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

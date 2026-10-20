using ValheimGen;

namespace TerrainEditor.App;

// The location kinds from the location files' entries (expand_locations.yaml), and Expand World Size's changes to the
// game's own kinds.
public static partial class ExpandWorld
{
    /// <summary>The game's start location, which the locations multiplier leaves alone and the mod forces when missing.</summary>
    private const string StartLocation = "StartTemple";

    /// <summary>
    /// The location kinds from the location files' entries, in file order, as the mod's FromData makes them: each
    /// entry's prefab is one of the game's (whose radii it takes unless the entry gives its own), a clone of one
    /// ("Name:Tag", laid out as its own kind with its own draws) or a blueprint file in the blueprint folder (no prefab:
    /// its radius from the pieces, plus 5 m); any other is passed over and noted. Distances are fractions of the radius,
    /// the biomes and areas names, the groups and the "awayFrom"/"closeTo" rules as the mod keeps them.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="game">The game's kinds (for the radii of their prefabs).</param>
    /// <param name="biomes">The world's biomes (for the names).</param>
    /// <param name="radius">The world's radius, metres.</param>
    /// <param name="blueprints">The folder the mod reads blueprint files from; null for none.</param>
    /// <param name="notes">Where an entry passed over or a value not understood is noted.</param>
    /// <param name="data">The world's data entries (what the contents' "data" fields name); none by default.</param>
    internal static List<SeedLocations.Rule> LocationsOf(IReadOnlyList<Entry> entries, IReadOnlyList<SeedLocations.Rule> game, BiomeTable biomes, float radius, string? blueprints, List<string> notes, DataTable? data = null)
    {
        List<SeedLocations.Rule> rules = [];
        var byPrefab = new Dictionary<string, SeedLocations.Rule>(StringComparer.Ordinal);
        foreach (var g in game)
        {
            byPrefab.TryAdd(g.Prefab, g);
        }
        int virtualGroups = 0;
        foreach (var entry in entries)
        {
            var f = new Fields(entry, "locations", notes);
            string prefab = f.Str("prefab").Trim();
            if (prefab == "")
            {
                continue;
            }
            string baseName = prefab.Split(':')[0];
            float interior = f.F("interiorRadius", 0f), exterior = f.F("exteriorRadius", 0f);
            bool blueprint = false;
            if (byPrefab.TryGetValue(baseName, out var own))
            {
                interior = interior > 0f ? interior : own.InteriorRadius;
                exterior = exterior != 0f ? exterior : own.ExteriorRadius;
            }
            else if (BlueprintRadius(blueprints, prefab, notes) is { } bpRadius)
            {
                blueprint = true;
                exterior = exterior != 0f ? exterior : bpRadius + 5f;
            }
            else
            {
                notes.Add($"expand_locations: location prefab {baseName} not found (left out)");
                continue;
            }
            var rule = LocationOf(f, prefab, baseName, interior, exterior, blueprint, biomes, radius, ref virtualGroups, notes);
            rule.ClearArea = ClearAreaOf(f, own, blueprint);
            rule.Contents = rule.Custom == true || blueprint ? ContentsOf(f, data ?? DataTable.Empty, blueprint, rule.ExteriorRadius) : null;
            rules.Add(rule);
        }
        return rules;
    }

    /// <summary>
    /// Whether a kind clears its area: the file's "clearArea" only with a radius of the file's own (or for a blueprint),
    /// else the game's prefab's, as the mod applies it.
    /// </summary>
    private static int ClearAreaOf(Fields f, SeedLocations.Rule? own, bool blueprint) =>
        f.F("exteriorRadius", 0f) != 0f || blueprint ? Flag(f, "clearArea") : own?.ClearArea ?? 0;

    /// <summary>One kind from its entry.</summary>
    private static SeedLocations.Rule LocationOf(Fields f, string prefab, string baseName, float interior, float exterior, bool blueprint, BiomeTable biomes, float radius, ref int virtualGroups, List<string> notes)
    {
        string group = f.Str("group"), groupMax = f.Str("groupMax"), altBiome = f.Str("altBiome");
        // The groups the kind belongs to, with its own group names as the file gives them (before any made-up one).
        List<string> groups = [.. SplitList(f.Str("groups"))];
        foreach (string g in new[] { group, groupMax }.Where(g => g != "" && !groups.Contains(g)))
        {
            groups.Add(g);
        }
        float minSimilar = f.F("minDistanceFromSimilar", 0f), maxSimilar = f.F("maxDistanceFromSimilar", 0f);
        var awayFrom = DistanceRows(f, "awayFrom", notes)?.SelectMany(row => row).ToList();
        var closeTo = DistanceRows(f, "closeTo", notes);
        if (blueprint)
        {
            // Blueprints share no prefab: the mod keeps them apart by name.
            if (awayFrom == null && minSimilar > 0f)
            {
                awayFrom = [new SeedLocations.DistanceRule(prefab, minSimilar), .. group == "" ? [] : new[] { new SeedLocations.DistanceRule(group, minSimilar) }];
            }
            if (closeTo == null && maxSimilar > 0f)
            {
                closeTo = [[new SeedLocations.DistanceRule(prefab, maxSimilar), .. groupMax == "" ? [] : new[] { new SeedLocations.DistanceRule(groupMax, maxSimilar) }]];
            }
        }
        if (awayFrom != null)
        {
            group = $"_{++virtualGroups}";
            minSimilar = 1f;
        }
        if (closeTo != null)
        {
            groupMax = $"_{++virtualGroups}";
            maxSimilar = 1f;
        }
        return new SeedLocations.Rule
        {
            Prefab = prefab,
            Name = prefab,
            BasePrefab = baseName == prefab ? null : baseName,
            Enable = Bit(f, "enabled", true),
            Biome = (int)biomes.Parse(f.Str("biome"), name => notes.Add($"expand_locations: {name} is no biome (left out)")),
            BiomeArea = AreasOf(f, "biomeArea", notes),
            Quantity = Int(f.Str("quantity")) ?? 0,
            Prioritized = Flag(f, "prioritized"),
            CenterFirst = Flag(f, "centerFirst"),
            Unique = Flag(f, "unique"),
            Group = group,
            MinDistanceFromSimilar = minSimilar,
            GroupMax = groupMax,
            MaxDistanceFromSimilar = maxSimilar,
            InteriorRadius = interior,
            ExteriorRadius = exterior,
            MinTerrainDelta = f.F("minTerrainDelta", 0f),
            MaxTerrainDelta = f.F("maxTerrainDelta", 10f),
            MinimumVegetation = f.F("minVegetation", 0f),
            MaximumVegetation = f.F("maxVegetation", 1f),
            SurroundCheckVegetation = Flag(f, "surroundCheckVegetation"),
            SurroundCheckDistance = f.F("surroundCheckDistance", 20f),
            SurroundCheckLayers = Int(f.Str("surroundCheckLayers")) ?? 2,
            SurroundBetterThanAverage = f.F("surroundBetterThanAverage", 0f),
            InForest = Flag(f, "inForest"),
            ForestTresholdMin = f.F("forestTresholdMin", 0f),
            ForestTresholdMax = f.F("forestTresholdMax", 1f),
            MinDistance = Dist(f.F("minDistance", 0f), radius),
            MaxDistance = Dist(f.F("maxDistance", 0f), radius),
            MinAltitude = f.F("minAltitude", -1000f),
            MaxAltitude = f.F("maxAltitude", 10000f),
            SlopeRotation = Flag(f, "slopeRotation"),
            RandomRotation = Flag(f, "randomRotation"),
            SnapToWater = Flag(f, "snapToWater"),
            AltBiome = string.IsNullOrWhiteSpace(altBiome) ? null : altBiome,
            Groups = groups.Count > 0 ? groups : null,
            AwayFrom = awayFrom,
            CloseTo = closeTo,
            Blueprint = OrNull(blueprint),
            Custom = OrNull(CustomFields.Any(f.Has) || f.Str("levelArea") is not ("" or "false")),
            RandomDamage = f.Str("randomDamage"),
        };
    }
}

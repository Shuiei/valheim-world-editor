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
    internal static List<SeedLocations.Rule> LocationsOf(IReadOnlyList<Entry> entries, IReadOnlyList<SeedLocations.Rule> game, BiomeTable biomes, float radius, string? blueprints, List<string> notes)
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
            rules.Add(LocationOf(f, prefab, baseName, interior, exterior, blueprint, biomes, radius, ref virtualGroups, notes));
        }
        return rules;
    }

    /// <summary>One kind from its entry.</summary>
    private static SeedLocations.Rule LocationOf(Fields f, string prefab, string baseName, float interior, float exterior, bool blueprint, BiomeTable biomes, float radius, ref int virtualGroups, List<string> notes)
    {
        float Dist(float fraction) => (float)((double)fraction * radius);
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
            Enable = f.B("enabled", true) ? 1 : 0,
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
            MinDistance = Dist(f.F("minDistance", 0f)),
            MaxDistance = Dist(f.F("maxDistance", 0f)),
            MinAltitude = f.F("minAltitude", -1000f),
            MaxAltitude = f.F("maxAltitude", 10000f),
            SlopeRotation = Flag(f, "slopeRotation"),
            RandomRotation = Flag(f, "randomRotation"),
            SnapToWater = Flag(f, "snapToWater"),
            AltBiome = string.IsNullOrWhiteSpace(altBiome) ? null : altBiome,
            Groups = groups.Count > 0 ? groups : null,
            AwayFrom = awayFrom,
            CloseTo = closeTo,
            Blueprint = blueprint ? true : null,
            Custom = CustomFields.Any(f.Has) || f.Str("levelArea") is not ("" or "false") ? true : null,
        };
    }

    /// <summary>The entry fields that change what a location spawns (its contents cannot be known then).</summary>
    private static readonly string[] CustomFields = ["dungeon", "randomSeed", "data", "objectData", "objectSwap", "dungeonObjectData", "dungeonObjectSwap", "locationObjectData", "locationObjectSwap", "objects", "commands", "paint", "offset", "groundOffset", "scaleMin", "scaleMax", "randomCardinal", "randomDamage"];

    /// <summary>A true-or-false field as the rules keep one (1 or 0).</summary>
    private static int Flag(Fields f, string key) => f.B(key, false) ? 1 : 0;

    /// <summary>
    /// A biome area list as the mod reads one ("edge", "median", a comma list of them; empty: both); a name that is
    /// neither is noted and passed over.
    /// </summary>
    private static int AreasOf(Fields f, string key, List<string> notes)
    {
        string text = f.Str(key);
        if (text == "")
        {
            return (int)Heightmap.BiomeArea.Everything;
        }
        int areas = 0;
        foreach (string name in SplitList(text))
        {
            if (Enum.TryParse<Heightmap.BiomeArea>(name, true, out var area) && area != Heightmap.BiomeArea.Everything)
            {
                areas |= (int)area;
            }
            else
            {
                notes.Add($"expand_{f.Kind}: {name} is no biome area (left out)");
            }
        }
        return areas;
    }

    /// <summary>
    /// The mod's distance rules of a field ("awayFrom", "closeTo"): a text or a list of texts, each a row of names
    /// followed by the distance that applies to them ("Name, Group, 500, Other, 1000"); a distance without names, or
    /// names without a distance, are noted and left out. Null when the field is missing or gives no rule.
    /// </summary>
    private static List<List<SeedLocations.DistanceRule>>? DistanceRows(Fields f, string key, List<string> notes)
    {
        List<List<SeedLocations.DistanceRule>> rows = [];
        foreach (string item in f.Rows(key))
        {
            List<SeedLocations.DistanceRule> row = [];
            List<string> names = [];
            foreach (string token in SplitList(item))
            {
                if (Float(token) is { } distance)
                {
                    if (names.Count == 0)
                    {
                        notes.Add($"expand_locations: {key} \"{item}\": the distance {token} has no names (left out)");
                    }
                    row.AddRange(names.Select(n => new SeedLocations.DistanceRule(n, distance)));
                    names.Clear();
                }
                else
                {
                    names.Add(token);
                }
            }
            if (names.Count > 0)
            {
                notes.Add($"expand_locations: {key} \"{item}\": no distance for {string.Join(", ", names)} (left out)");
            }
            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }
        return rows.Count == 0 ? null : rows;
    }

    /// <summary>
    /// The radius of a blueprint location: half the diagonal of its pieces' bounds on the ground, as the mod measures
    /// it, from the blueprint file named by the prefab (with .blueprint or .vbuild) in the blueprint folder; null when
    /// there is no such file, or it cannot be read (noted).
    /// </summary>
    internal static float? BlueprintRadius(string? folder, string prefab, List<string> notes)
    {
        if (folder == null || !Directory.Exists(folder))
        {
            return null;
        }
        string? file = BlueprintExtensions.Select(ext => Path.Combine(folder, prefab + ext)).FirstOrDefault(File.Exists);
        if (file == null)
        {
            return null;
        }
        try
        {
            var parsed = BlueprintFormats.Parse(Path.GetFileName(file), File.ReadAllText(file));
            if (parsed.Pieces.Count == 0)
            {
                return 0f;
            }
            float minX = parsed.Pieces.Min(p => p.Position.X), maxX = parsed.Pieces.Max(p => p.Position.X);
            float minZ = parsed.Pieces.Min(p => p.Position.Z), maxZ = parsed.Pieces.Max(p => p.Position.Z);
            float ex = (maxX - minX) / 2f, ez = (maxZ - minZ) / 2f;
            return (float)Math.Sqrt((double)ex * ex + (double)ez * ez);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            Log.Caught(ex, $"reading the blueprint {file} for its radius (the location is left out)");
            notes.Add($"expand_locations: the blueprint {Path.GetFileName(file)} could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>The file name extensions of the blueprint files the mod reads.</summary>
    private static readonly string[] BlueprintExtensions = [".blueprint", ".vbuild"];

    /// <summary>
    /// The game's own kinds as Expand World Size changes them, when it does: each enabled kind's quantity (but the
    /// start's) times its locations multiplier, rounded, at least 1 (0 for a multiplier of 0); and, without Expand
    /// World Data, their distances from the middle scaled with the radius. The kinds as given when nothing changes.
    /// </summary>
    /// <param name="rules">The kinds.</param>
    /// <param name="r">The world's rules.</param>
    internal static List<SeedLocations.Rule> SizedLocations(IReadOnlyList<SeedLocations.Rule> rules, WorldRules r)
    {
        bool quantities = r.LocationsMultiplier != 1f, distances = !r.DataInstalled && r.Radius != 10000f;
        if (!quantities && !distances)
        {
            return [.. rules];
        }
        List<SeedLocations.Rule> sized = [];
        foreach (var rule in rules)
        {
            var copy = SeedLocations.RuleSet.FromJson("{\"locations\":[" + System.Text.Json.JsonSerializer.Serialize(rule) + "]}").Locations[0];
            if (quantities && copy.Quantity != 0 && copy.Enable != 0 && copy.Prefab != StartLocation)
            {
                copy.Quantity = r.LocationsMultiplier == 0f ? 0 : Math.Max(1, (int)Math.Round((double)copy.Quantity * r.LocationsMultiplier, MidpointRounding.ToEven));
            }
            if (distances)
            {
                copy.MinDistance = (float)((double)copy.MinDistance * (r.Radius / 10000f));
                copy.MaxDistance = (float)((double)copy.MaxDistance * (r.Radius / 10000f));
            }
            sized.Add(copy);
        }
        return sized;
    }
}

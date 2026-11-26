using System.Globalization;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace ExpandWorld.WorldEditor;

// Expand World Prefabs' rules (expand_prefabs*.yaml) as the Prefab rules tool shows them: what starts each one, the
// objects it acts on, what it does, and which of its conditions the editor can check on an object of the world.
// Nothing here runs a rule: the editor only says which objects a rule would act on, and how sure it is.
public static partial class ExpandWorldMod
{
    /// <summary>How sure the editor is that a rule acts on an object.</summary>
    public enum RuleMatch
    {
        /// <summary>A condition the editor checks fails: the rule leaves the object alone.</summary>
        No,

        /// <summary>Every condition the editor checks holds, but the rule has some only the game knows.</summary>
        Maybe,

        /// <summary>Every condition holds, and the rule has none the editor cannot check.</summary>
        Yes,
    }

    /// <summary>A data filter of a rule ("int, key, value"), as far as the editor can check it on an object.</summary>
    /// <param name="Type">The value's type ("int", "string"…).</param>
    /// <param name="Key">The key.</param>
    /// <param name="Value">The value asked, as written.</param>
    /// <param name="Banned">A banned filter: an object with this value is left alone.</param>
    public sealed record RuleFilter(string Type, string Key, string Value, bool Banned);

    /// <summary>An Expand World Prefabs rule, as the editor reads it.</summary>
    public sealed record PrefabRule
    {
        /// <summary>Its place among the rules (1: the first), for the player to find it in the files.</summary>
        public int Number { get; init; }

        /// <summary>What starts it ("create", "destroy", "say, check"…), as written; several for "types".</summary>
        public IReadOnlyList<string> Triggers { get; init; } = [];

        /// <summary>Whether one of its triggers is an object being made ("create"): the only one a world's making meets.</summary>
        public bool OnCreate => Triggers.Any(t => FirstWord(t).Equals("create", StringComparison.OrdinalIgnoreCase));

        /// <summary>The objects it is for, as written ("prefab": names, wildcards, value groups).</summary>
        public string Prefab { get; init; } = "";

        /// <summary>The game objects it names outright (those the editor marks).</summary>
        public IReadOnlyList<string> Prefabs { get; init; } = [];

        /// <summary>What the editor cannot list objects for: wildcards ("Boar*"), value groups ("&lt;trees&gt;"), unknown names.</summary>
        public IReadOnlyList<string> Unlisted { get; init; } = [];

        /// <summary>The objects left out ("excludePrefab").</summary>
        public IReadOnlyList<string> Excluded { get; init; } = [];

        /// <summary>The biomes it is for (null: every biome, or names the editor does not know).</summary>
        public Heightmap.Biome? Biomes { get; init; }

        /// <summary>The biomes it is not for.</summary>
        public Heightmap.Biome BannedBiomes { get; init; }

        /// <summary>Distance from the world's middle, metres (a value below 1 is a fraction of 10 000 m, as the mod reads it).</summary>
        public float? MinDistance { get; init; }

        /// <inheritdoc cref="MinDistance"/>
        public float? MaxDistance { get; init; }

        /// <summary>Height above the sea (30 m), metres.</summary>
        public float? MinAltitude { get; init; }

        /// <inheritdoc cref="MinAltitude"/>
        public float? MaxAltitude { get; init; }

        /// <summary>Bounds of the object's own position, metres.</summary>
        public float? MinX { get; init; }

        /// <inheritdoc cref="MinX"/>
        public float? MaxX { get; init; }

        /// <inheritdoc cref="MinX"/>
        public float? MinY { get; init; }

        /// <inheritdoc cref="MinX"/>
        public float? MaxY { get; init; }

        /// <inheritdoc cref="MinX"/>
        public float? MinZ { get; init; }

        /// <inheritdoc cref="MinX"/>
        public float? MaxZ { get; init; }

        /// <summary>Whether it acts beside the rule drawn among an object's ("separate"), not drawn with them.</summary>
        public bool Separate { get; init; }

        /// <summary>Whether it acts only when no other rule of its trigger does ("fallback").</summary>
        public bool Fallback { get; init; }

        /// <summary>The data filters the editor checks (plain "type, key, value").</summary>
        public IReadOnlyList<RuleFilter> Filters { get; init; } = [];

        /// <summary>The conditions only the game knows (the field names: "environments", "objects", "chance"…).</summary>
        public IReadOnlyList<string> GameOnly { get; init; } = [];

        /// <summary>What it does, for the player ("removes it", "spawns vfx_spawn"…).</summary>
        public IReadOnlyList<string> Actions { get; init; } = [];

        /// <summary>What the editor does when it applies the rule as an object is made (null: it does not; see <see cref="NotApplied"/>).</summary>
        public CreatePlan? Create { get; init; }

        /// <summary>Why the editor does not apply the rule as objects are made ("": it does).</summary>
        public string NotApplied { get; init; } = "";

        /// <summary>One line for the rule's list: number, trigger, objects.</summary>
        public string Title => $"#{Number} {string.Join(" / ", Triggers.Select(FirstWord))}: {(Prefab.Length > 0 ? Prefab : "(no prefab)")}";
    }

    private static string FirstWord(string trigger) => trigger.Split(',')[0].Trim();

    /// <summary>The fields of a rule that hold conditions only the game knows: the world as it is played.</summary>
    private static readonly string[] GameConditions =
    [
        "environments", "bannedEnvironments", "globalKeys", "bannedGlobalKeys", "keys", "bannedKeys", "events",
        "eventDistance",
        "objects", "bannedObjects", "locations", "bannedLocations", "playerEvents", "bannedPlayerEvents", "groups",
        "bannedGroups", "day", "night", "paint", "minPaint", "maxPaint", "terrainHeight", "minTerrainHeight",
        "maxTerrainHeight", "chance", "weight", "condition", "admin", "filterLimit",
    ];

    /// <summary>Expand World Prefabs' rules among the entries of its files, in file order.</summary>
    /// <param name="scripts">The entries of expand_prefabs*.yaml (data entries among them are passed over).</param>
    /// <param name="known">Whether a name is an object of the game.</param>
    /// <param name="biomes">The world's biomes, for the names.</param>
    internal static List<PrefabRule> PrefabRulesOf(IReadOnlyList<Entry> scripts, Func<string, bool> known, BiomeTable biomes)
    {
        List<PrefabRule> rules = [];
        foreach (var entry in scripts.Where(IsRule))
        {
            var f = entry.Fields;
            string Text(string key) => f.GetValueOrDefault(key) is string s ? s.Trim() : "";
            float? Num(string key, bool fraction = false) =>
                float.TryParse(Text(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? (fraction && v < 1f ? v * 10000f : v) : null;
            List<string> gameOnly = [.. GameConditions.Where(k => f.TryGetValue(k, out object? v) && v != null && !(v is string s && s.Trim() == ""))];
            // A bound that is no plain number (a parameter, a calculation) is the game's to work out.
            foreach (string key in new[] { "minDistance", "maxDistance", "minAltitude", "maxAltitude", "minX", "maxX", "minY", "maxY", "minZ", "maxZ" })
            {
                if (Text(key) != "" && Num(key) == null)
                {
                    gameOnly.Add(key);
                }
            }
            Heightmap.Biome? Biomes(string key, bool banned)
            {
                if (Text(key) == "")
                {
                    return banned ? Heightmap.Biome.None : null;
                }
                bool unknown = false;
                var value = biomes.Parse(Text(key), _ => unknown = true);
                if (unknown)
                {
                    // An alt biome or a name the editor does not know: only the game can tell.
                    gameOnly.Add(key);
                    return banned ? Heightmap.Biome.None : null;
                }
                return value;
            }
            List<RuleFilter> filters = [];
            void Filters(string key, bool banned)
            {
                foreach (string row in Rows(f.GetValueOrDefault(key)))
                {
                    string[] p = [.. row.Split(',').Select(s => s.Trim())];
                    // A weight (a fourth part), a range, a parameter or a named entry: the game's to work out.
                    if (p.Length == 3 && ListOfType.ContainsKey(p[0]) && !p[2].Contains('<', StringComparison.Ordinal) && !p[2].Contains(';', StringComparison.Ordinal) && !p[2].Contains('*', StringComparison.Ordinal))
                    {
                        filters.Add(new RuleFilter(p[0].ToLowerInvariant(), p[1], p[2], banned));
                    }
                    else if (!gameOnly.Contains(key))
                    {
                        gameOnly.Add(key);
                    }
                }
            }
            Filters("filter", false);
            Filters("filters", false);
            Filters("bannedFilter", true);
            Filters("bannedFilters", true);
            List<string> named = [.. Rows(f.GetValueOrDefault("prefab")).SelectMany(SplitList)];
            var biome = Biomes("biomes", false);
            var banned = Biomes("bannedBiomes", true) ?? Heightmap.Biome.None;
            var rule = new PrefabRule
            {
                Number = rules.Count + 1,
                Triggers = [.. Rows(f.GetValueOrDefault("types")).Concat(Rows(f.GetValueOrDefault("type"))).Select(t => t.Trim()).Where(t => t != "").DefaultIfEmpty("create")],
                Prefab = string.Join(", ", named),
                Prefabs = [.. named.Where(n => !n.Contains('*', StringComparison.Ordinal) && known(n)).Distinct(StringComparer.Ordinal)],
                Unlisted = [.. named.Where(n => n.Contains('*', StringComparison.Ordinal) || !known(n)).Distinct(StringComparer.Ordinal)],
                Excluded = [.. Rows(f.GetValueOrDefault("excludePrefab")).SelectMany(SplitList)],
                Biomes = biome,
                BannedBiomes = banned,
                MinDistance = Num("minDistance", true),
                MaxDistance = Num("maxDistance", true),
                MinAltitude = Num("minAltitude"),
                MaxAltitude = Num("maxAltitude"),
                MinX = Num("minX"),
                MaxX = Num("maxX"),
                MinY = Num("minY"),
                MaxY = Num("maxY"),
                MinZ = Num("minZ"),
                MaxZ = Num("maxZ"),
                Separate = Text("separate") == "true",
                Fallback = Text("fallback") == "true",
                Filters = filters,
                GameOnly = gameOnly,
                Actions = ActionsOf(f),
            };
            var (plan, why) = PlanOf(rule, f, known);
            rules.Add(rule with { Create = plan, NotApplied = why });
        }
        return rules;
    }

    /// <summary>What a rule does, in words.</summary>
    private static List<string> ActionsOf(IReadOnlyDictionary<string, object?> f)
    {
        List<string> actions = [];
        string Text(string key) => f.GetValueOrDefault(key) is string s ? s.Trim() : "";
        bool Has(string key) => f.TryGetValue(key, out object? v) && v != null && !(v is string s && s.Trim() == "");
        IEnumerable<string> Spawned(string key)
        {
            foreach (object? item in f.GetValueOrDefault(key) is List<object?> list ? list : [f.GetValueOrDefault(key)])
            {
                if (item is Dictionary<string, object?> map && map.GetValueOrDefault("prefab") is string p)
                {
                    yield return p.Trim();
                }
                else if (item is string line && line.Trim() != "")
                {
                    yield return line.Split(',')[0].Trim();
                }
            }
        }
        List<string> swaps = [.. Spawned("swap").Concat(Spawned("swaps"))];
        if (swaps.Count > 0)
        {
            actions.Add($"swaps it for {string.Join(", ", swaps)}");
        }
        else if (Has("remove") && Text("remove") != "false")
        {
            actions.Add(Text("remove") is "true" or "" ? "removes it" : $"removes it ({Text("remove")})");
        }
        List<string> spawns = [.. Spawned("spawn").Concat(Spawned("spawns"))];
        if (spawns.Count > 0)
        {
            actions.Add($"spawns {string.Join(", ", spawns)}");
        }
        if (Text("data") != "")
        {
            actions.Add($"sets data {Text("data")}");
        }
        else if (f.GetValueOrDefault("data") is Dictionary<string, object?>)
        {
            actions.Add("sets data (written in the rule)");
        }
        (string Key, string Words)[] others =
        [
            ("drops", "drops items"), ("addItems", "adds items"), ("removeItems", "removes items"),
            ("command", "runs commands"), ("commands", "runs commands"), ("exec", "runs functions"),
            ("poke", "pokes objects"), ("pokes", "pokes objects"), ("terrain", "changes the terrain"),
            ("objectRpc", "calls the object"), ("clientRpc", "calls the players"), ("owner", "changes the owner"),
            ("attach", "attaches it"), ("connect", "connects it"), ("cancel", "cancels what started it"),
        ];
        foreach (var (key, words) in others)
        {
            if (Has(key) && !actions.Contains(words))
            {
                actions.Add(words);
            }
        }
        return actions;
    }

    /// <summary>Whether a rule acts on an object, by the conditions the editor can check (its trigger aside).</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="obj">The object.</param>
    /// <param name="biome">The biome under it (Heightmap.Biome).</param>
    /// <param name="drawn">Whether the rule's chance is drawn by the editor (applying it): no condition the game keeps.</param>
    internal static RuleMatch Match(PrefabRule rule, IObjectView obj, int biome, bool drawn = false)
    {
        if (!rule.Prefabs.Contains(obj.Prefab) || rule.Excluded.Contains(obj.Prefab))
        {
            return RuleMatch.No;
        }
        var b = (Heightmap.Biome)biome;
        if ((rule.Biomes is { } wanted && (wanted & b) != b) || (rule.BannedBiomes & b) != 0)
        {
            return RuleMatch.No;
        }
        var p = obj.Position;
        float distance = MathF.Sqrt((p.X * p.X) + (p.Z * p.Z)), altitude = p.Y - 30f;
        static bool Within(float v, float? min, float? max) => (min == null || v >= min) && (max == null || v < max);
        if (!Within(distance, rule.MinDistance, rule.MaxDistance) || !Within(altitude, rule.MinAltitude, rule.MaxAltitude)
            || !Within(p.X, rule.MinX, rule.MaxX) || !Within(p.Y, rule.MinY, rule.MaxY) || !Within(p.Z, rule.MinZ, rule.MaxZ))
        {
            return RuleMatch.No;
        }
        bool unsure = rule.GameOnly.Any(k => !(drawn && k == "chance"));
        foreach (var filter in rule.Filters)
        {
            bool? holds = Holds(filter, obj);
            if (holds == null)
            {
                unsure = true;
            }
            else if (holds != !filter.Banned)
            {
                // With a limit, the game counts the filters that hold instead of asking for each.
                if (!rule.GameOnly.Contains("filterLimit"))
                {
                    return RuleMatch.No;
                }
                unsure = true;
            }
        }
        return unsure ? RuleMatch.Maybe : RuleMatch.Yes;
    }

    /// <summary>
    /// Whether a rule acts on an object among the other rules: as <see cref="Match"/>, but a rule the game draws among
    /// others of its trigger (a plain one beside another plain one), or a "fallback" one another rule may take the
    /// place of, is only Maybe.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="all">Every rule of the files.</param>
    /// <param name="obj">The object.</param>
    /// <param name="biome">The biome under it (Heightmap.Biome).</param>
    internal static RuleMatch MatchAmong(PrefabRule rule, IReadOnlyList<PrefabRule> all, IObjectView obj, int biome)
    {
        var match = Match(rule, obj, biome);
        if (match != RuleMatch.Yes || rule.Separate)
        {
            return match;
        }
        var triggers = rule.Triggers.Select(FirstWord).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Competes(PrefabRule other) => !ReferenceEquals(other, rule) && !other.Separate && (rule.Fallback || !other.Fallback)
            && other.Triggers.Any(t => triggers.Contains(FirstWord(t))) && Match(other, obj, biome) != RuleMatch.No;
        return all.Any(Competes) ? RuleMatch.Maybe : RuleMatch.Yes;
    }

    /// <summary>
    /// Whether an object has a filter's value. A value the object does not hold reads as the game's default (0, false,
    /// ""), as the game reads it; null for a value written as the editor cannot compare it.
    /// </summary>
    private static bool? Holds(RuleFilter filter, IObjectView obj)
    {
        string key = filter.Key, text = filter.Value;
        var inv = CultureInfo.InvariantCulture;
        int Int() => obj.TryGetInt(key, out int v) ? v : 0;
        return filter.Type switch
        {
            "int" => int.TryParse(text, NumberStyles.Integer, inv, out int w) ? Int() == w : null,
            "bool" => bool.TryParse(text, out bool wb) ? (Int() != 0) == wb : null,
            "hash" => Int() == GameHash.Of(text),
            "long" => long.TryParse(text, NumberStyles.Integer, inv, out long wl) ? (obj.TryGetLong(key, out long l) ? l : 0) == wl : null,
            "float" => float.TryParse(text, NumberStyles.Float, inv, out float wf) ? Math.Abs((obj.TryGetFloat(key, out float fl) ? fl : 0f) - wf) < 1e-4f : null,
            "string" => (obj.TryGetString(key, out string s) ? s : "") == (text.Length > 1 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text),
            _ => null,
        };
    }
}

// Reading the rules from the first BepInEx folder with Expand World Prefabs.
public static partial class ExpandWorldMod
{
    /// <summary>What was found of Expand World Prefabs' rules.</summary>
    /// <param name="Rules">The rules, in file order.</param>
    /// <param name="Problem">Why there are none, for the player ("": they were read).</param>
    public sealed record PrefabRules(IReadOnlyList<PrefabRule> Rules, string Problem);

    /// <summary>
    /// The rules of expand_prefabs*.yaml in the first BepInEx folder with Expand World Prefabs installed. Lines about
    /// the files go to the log.
    /// </summary>
    /// <param name="folders">The BepInEx folders the editor knows.</param>
    public static PrefabRules ReadPrefabRules(IEnumerable<BepInExFolder> folders)
    {
        foreach (var folder in folders)
        {
            string plugins = Path.Combine(folder.Path, "plugins");
            if (!HasPlugin(plugins, "ExpandWorldPrefabs.dll"))
            {
                continue;
            }
            var yaml = YamlReader.Find(plugins);
            if (yaml == null)
            {
                return new PrefabRules([], $"No {YamlReader.DllName} in the plugins folder: the rule files cannot be read.");
            }
            List<string> notes = [];
            string files = Path.Combine(folder.Path, "config", DataFolder);
            var scripts = ReadKind(Directory.Exists(files) ? files : null, "prefabs", yaml, notes) ?? [];
            foreach (string note in notes)
            {
                Note($"Expand World: {note}");
            }
            var rules = PrefabRulesOf(scripts, Game.HasPrefab, BiomeTable.Vanilla);
            return new PrefabRules(rules, rules.Count > 0 ? "" : "No rule found in config/expand_world (expand_prefabs*.yaml).");
        }
        return new PrefabRules([], "Expand World Prefabs is not installed in the BepInEx folders the editor knows.");
    }
}

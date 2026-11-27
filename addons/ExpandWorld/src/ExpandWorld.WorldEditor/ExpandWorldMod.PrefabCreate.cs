using System.Globalization;
using System.Numerics;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace ExpandWorld.WorldEditor;

// Expand World Prefabs' "create" rules applied as the editor makes objects (those a step adds): which
// rules an object meets, the one drawn among them, and what it does (swap, spawn, remove, data), as the mod does when
// the game creates the object. Only rules whose every condition the editor can check are applied; the draws (a rule's
// weight and chance, a spawn's) come from the world's seed and the object's place, so a step made again comes out the
// same.
public static partial class ExpandWorldMod
{
    /// <summary>An object a rule spawns or swaps in.</summary>
    /// <param name="Prefab">Its prefab.</param>
    /// <param name="Offset">Where it stands from the object, turned with it (x, y, z; the mod writes x, z, y).</param>
    /// <param name="Turn">Its turn over the object's.</param>
    /// <param name="Data">The data entry it is given ("": none).</param>
    /// <param name="Weight">Its weight among the weighted ones (null: always made).</param>
    /// <param name="Chance">The chance it is made (null: always).</param>
    /// <param name="Snap">Whether it stands on the ground (its offset's height over it).</param>
    public sealed record SpawnPlan(string Prefab, Vector3 Offset, Quaternion Turn, string Data, float? Weight, float? Chance, bool Snap);

    /// <summary>What a rule does as an object is made, as far as the editor applies it.</summary>
    public sealed record CreatePlan
    {
        /// <summary>Its weight among the rules an object meets (null: 1).</summary>
        public float? Weight { get; init; }

        /// <summary>The chance it acts once chosen (null: always).</summary>
        public float? Chance { get; init; }

        /// <summary>Whether it acts beside the rule drawn ("separate"), not drawn among them.</summary>
        public bool Separate { get; init; }

        /// <summary>Whether it acts only when no other rule does ("fallback").</summary>
        public bool Fallback { get; init; }

        /// <summary>Whether the object is removed ("remove", or a swap).</summary>
        public bool Remove { get; init; }

        /// <summary>The data entry the object is given ("": none).</summary>
        public string Data { get; init; } = "";

        /// <summary>The objects swapped in.</summary>
        public IReadOnlyList<SpawnPlan> Swaps { get; init; } = [];

        /// <summary>The objects spawned beside it.</summary>
        public IReadOnlyList<SpawnPlan> Spawns { get; init; } = [];

        /// <summary>What it also does that only the game does (drops, commands…), for the card.</summary>
        public IReadOnlyList<string> LeftToGame { get; init; } = [];
    }

    /// <summary>The fields of a rule's actions the editor does not do (the game does them as it is played).</summary>
    private static readonly string[] GameActions =
    [
        "drops", "addItems", "removeItems", "command", "commands", "exec", "poke", "pokes", "terrain", "objectRpc",
        "clientRpc", "owner", "attach", "connect", "cancel", "log", "logFile",
    ];

    /// <summary>How the editor applies a rule as objects are made; null and why when it does not.</summary>
    private static (CreatePlan? Plan, string Why) PlanOf(PrefabRule rule, IReadOnlyDictionary<string, object?> f, Func<string, bool> known,
        IReadOnlyDictionary<string, CustomData>? entries)
    {
        if (!rule.OnCreate)
        {
            return (null, "it runs while the game is played");
        }
        string Text(string key) => f.GetValueOrDefault(key) is string s ? s.Trim() : "";
        static float? Plain(string text) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : null;
        // A chance and a weight are drawn here (when plain numbers); any other condition only the game knows keeps the
        // rule from the objects the editor makes.
        var gameOnly = rule.GameOnly.Where(k => k is not ("chance" or "weight") || Plain(Text(k)) == null).ToList();
        if (gameOnly.Count > 0)
        {
            return (null, $"only the game checks {string.Join(", ", gameOnly)}");
        }
        if (rule.Unlisted.Count > 0 && rule.Prefabs.Count == 0)
        {
            return (null, "it names no object the editor knows");
        }
        string remove = Text("remove");
        if (remove is not ("" or "true" or "false"))
        {
            return (null, $"\"remove: {remove}\" is the game's to work out");
        }
        string data = Text("data");
        if (data.Contains('<', StringComparison.Ordinal) || (f.GetValueOrDefault("data") is not (null or string)))
        {
            return (null, "its data is written in the rule or worked out by the game");
        }
        var swaps = SpawnsOf(f, "swap", "swaps", known, out string? badSwap);
        var spawns = SpawnsOf(f, "spawn", "spawns", known, out string? badSpawn);
        if ((badSwap ?? badSpawn) is { } bad)
        {
            return (null, bad);
        }
        // A data entry the editor cannot give whole would leave the object, or what replaces it, part made.
        foreach (string name in new[] { data }.Concat(swaps.Concat(spawns).Select(s => s.Data)).Where(n => n != "").Distinct(StringComparer.Ordinal))
        {
            if (entries == null)
            {
                break;
            }
            if (entries.GetValueOrDefault(name) is not { } entry)
            {
                return (null, $"it gives the data entry {name}, which no data file holds");
            }
            if (entry.Skipped.Count > 0)
            {
                return (null, $"its data entry {name} sets what the game works out ({string.Join(", ", entry.Skipped)})");
            }
        }
        return (new CreatePlan
        {
            Weight = Plain(Text("weight")),
            Chance = Plain(Text("chance")),
            Separate = Text("separate") == "true",
            Fallback = Text("fallback") == "true",
            Remove = remove == "true" || swaps.Count > 0,
            Data = data,
            Swaps = swaps,
            Spawns = spawns,
            LeftToGame = [.. GameActions.Where(k => f.TryGetValue(k, out object? v) && v != null && !(v is string s && s.Trim() == ""))],
        }, "");
    }

    /// <summary>The objects a rule spawns or swaps in, from its lines ("prefab, x,z,y, y,x,z, data") or entries.</summary>
    private static List<SpawnPlan> SpawnsOf(IReadOnlyDictionary<string, object?> f, string one, string many, Func<string, bool> known, out string? bad)
    {
        bad = null;
        List<SpawnPlan> plans = [];
        foreach (object? item in new[] { one, many }.SelectMany(k => f.GetValueOrDefault(k) is List<object?> list ? list : [f.GetValueOrDefault(k)]))
        {
            SpawnPlan? plan = item switch
            {
                string line when line.Trim() != "" => SpawnLine(line),
                Dictionary<string, object?> map => SpawnEntry(map),
                _ => null,
            };
            if (item is null || (item is string s && s.Trim() == ""))
            {
                continue;
            }
            if (plan == null)
            {
                bad = $"a {one} of it is the game's to work out ({(item is string l ? l : "an entry")})";
                return [];
            }
            if (!known(plan.Prefab))
            {
                bad = $"it makes {plan.Prefab}, no object the editor can place (a blueprint, an effect or a mod's)";
                return [];
            }
            plans.Add(plan);
        }
        return plans;
    }

    /// <summary>A spawn line as the mod reads it: a prefab, then a position and a turn (three numbers each), a data entry, a delay or whether it triggers rules.</summary>
    private static SpawnPlan? SpawnLine(string line)
    {
        string[] p = [.. line.Split(',').Select(s => s.Trim())];
        if (p[0] == "" || p[0].Contains('<', StringComparison.Ordinal))
        {
            return null;
        }
        Vector3 offset = Vector3.Zero;
        var turn = Quaternion.Identity;
        string data = "";
        bool snap = false, hadPosition = false;
        static bool IsNum(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        // A position or rotation takes three fields; everything else one.
        for (int i = 1, step = 1; i < p.Length; i += step)
        {
            step = 1;
            if (bool.TryParse(p[i], out _))
            {
                // Whether the spawned object triggers rules: the editor never hands what it adds back.
                continue;
            }
            if (IsNum(p[i]))
            {
                if (p.Length <= i + 2 || !IsNum(p[i + 1]))
                {
                    // A delay: the object comes later, the same.
                    continue;
                }
                if (!hadPosition)
                {
                    snap = p[i + 2] == "snap";
                    if (Xzy(p[i], p[i + 1], snap ? "0" : p[i + 2]) is not { } at)
                    {
                        return null;
                    }
                    offset = at;
                    hadPosition = true;
                }
                else if (Yxz(p[i], p[i + 1], p[i + 2]) is { } t)
                {
                    turn = t;
                }
                else
                {
                    return null;
                }
                step = 3;
                continue;
            }
            if (p[i].Contains('<', StringComparison.Ordinal))
            {
                return null;
            }
            data = p[i];
        }
        return new SpawnPlan(p[0], offset, turn, data, null, null, snap);
    }

    /// <summary>The fields of a spawn entry only the game works out: repeats, conditions, what it is tied to.</summary>
    private static readonly string[] GameSpawnFields = ["repeat", "condition", "attach", "connect"];

    /// <summary>A spawn entry (prefab, pos, rot, data, weight, chance, snap); null when part of it is the game's to work out.</summary>
    private static SpawnPlan? SpawnEntry(Dictionary<string, object?> map)
    {
        string Text(string key) => map.GetValueOrDefault(key) is string s ? s.Trim() : "";
        // Repeats, conditions and owners are the game's.
        if (Text("prefab") is not { Length: > 0 } prefab || prefab.Contains('<', StringComparison.Ordinal)
            || GameSpawnFields.Any(k => Text(k) != "") || map.GetValueOrDefault("data") is not (null or string)
            || Text("data").Contains('<', StringComparison.Ordinal))
        {
            return null;
        }
        Vector3? Three(string a, string b) =>
            (Text(a) is { Length: > 0 } t ? t : Text(b)) is { Length: > 0 } v ? v.Split(',') is { Length: 3 } x ? Xzy(x[0], x[1], x[2]) : null : Vector3.Zero;
        Quaternion? Turn() =>
            (Text("rot") is { Length: > 0 } t ? t : Text("rotation")) is { Length: > 0 } v ? v.Split(',') is { Length: 3 } x ? Yxz(x[0], x[1], x[2]) : null : Quaternion.Identity;
        float? Num(string key) => float.TryParse(Text(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) ? n : null;
        if (Three("pos", "position") is not { } offset || Turn() is not { } turn || (Text("weight") != "" && Num("weight") == null) || (Text("chance") != "" && Num("chance") == null))
        {
            return null;
        }
        return new SpawnPlan(prefab, offset, turn, Text("data"), Num("weight"), Num("chance"), Text("snap") == "true");
    }

    /// <summary>A position the mod writes x, z, y.</summary>
    private static Vector3? Xzy(string x, string z, string y)
    {
        var ci = CultureInfo.InvariantCulture;
        return float.TryParse(x.Trim(), NumberStyles.Float, ci, out float a) && float.TryParse(z.Trim(), NumberStyles.Float, ci, out float b)
            && float.TryParse(y.Trim(), NumberStyles.Float, ci, out float c) ? new Vector3(a, c, b) : null;
    }

    /// <summary>A turn the mod writes y, x, z (degrees).</summary>
    private static Quaternion? Yxz(string y, string x, string z)
    {
        var ci = CultureInfo.InvariantCulture;
        return float.TryParse(y.Trim(), NumberStyles.Float, ci, out float a) && float.TryParse(x.Trim(), NumberStyles.Float, ci, out float b)
            && float.TryParse(z.Trim(), NumberStyles.Float, ci, out float c) ? Mathf.Euler(new Vector3(b, a, c)) : null;
    }

    /// <summary>
    /// Applies the create rules to a batch of objects being made: for each, the rules it meets (all their conditions
    /// checked here), one drawn by weight among the plain ones, every "separate" one, or a "fallback" one when none;
    /// then each acting rule, its chance drawn, removes or swaps the object, spawns beside it and gives it its data.
    /// </summary>
    /// <param name="made">The batch.</param>
    /// <param name="rules">The rules (only those with a plan act).</param>
    /// <param name="entries">The data entries, by name.</param>
    internal static void ApplyCreateRules(ICreation made, IReadOnlyList<PrefabRule> rules, IReadOnlyDictionary<string, CustomData> entries)
    {
        var acting = rules.Where(r => r.Create != null).ToList();
        foreach (var obj in made.Objects.ToList())
        {
            int biome = made.Biome(obj.Position.X, obj.Position.Z);
            var met = acting.Where(r => Match(r, obj, biome, drawn: true) == RuleMatch.Yes).ToList();
            if (met.Count == 0)
            {
                continue;
            }
            var random = new Random(Mix(made.Seed, obj.Position));
            List<PrefabRule> chosen = [.. met.Where(r => r.Create!.Separate)];
            if (Drawn([.. met.Where(r => !r.Create!.Separate && !r.Create.Fallback)], r => r.Create!.Weight, random) is { } one)
            {
                chosen.Insert(0, one);
            }
            if (chosen.Count == 0 && Drawn([.. met.Where(r => r.Create!.Fallback)], r => r.Create!.Weight, random) is { } fallback)
            {
                chosen.Add(fallback);
            }
            bool removed = false;
            foreach (var plan in chosen.Select(r => r.Create!))
            {
                if (plan.Chance is { } chance && chance < 1f && random.NextDouble() > chance)
                {
                    continue;
                }
                var data = plan.Data != "" ? entries.GetValueOrDefault(plan.Data) : null;
                foreach (var spawn in Spawned(plan.Spawns, random))
                {
                    Make(made, obj, spawn, data, entries);
                }
                foreach (var swap in Spawned(plan.Swaps, random))
                {
                    Make(made, obj, swap, data, entries);
                }
                if (plan.Remove)
                {
                    removed = true;
                }
                else if (data != null)
                {
                    CustomObjectTool.Apply(data, obj);
                }
            }
            if (removed)
            {
                made.Remove(obj);
            }
        }
    }

    /// <summary>The spawns made: every unweighted one (its chance drawn), and one drawn among the weighted ones.</summary>
    private static IEnumerable<SpawnPlan> Spawned(IReadOnlyList<SpawnPlan> spawns, Random random)
    {
        foreach (var s in spawns.Where(s => s.Weight == null))
        {
            if (s.Chance is not { } c || c >= 1f || random.NextDouble() <= c)
            {
                yield return s;
            }
        }
        // The one drawn still has its chance drawn, as every spawn does.
        if (Drawn([.. spawns.Where(s => s.Weight != null)], s => s.Weight, random) is { } one && (one.Chance is not { } chance || chance >= 1f || random.NextDouble() <= chance))
        {
            yield return one;
        }
    }

    /// <summary>
    /// One drawn by weight (1 when none) as the mod draws: out of the sum of the weights, or 1 when they come to less
    /// (so that weights below 1 leave room for none); a single one weighing 1 or more always.
    /// </summary>
    private static T? Drawn<T>(IReadOnlyList<T> among, Func<T, float?> weight, Random random) where T : class
    {
        if (among.Count == 0)
        {
            return null;
        }
        float[] w = [.. among.Select(a => weight(a) ?? 1f)];
        if (among.Count == 1 && w[0] >= 1f)
        {
            return among[0];
        }
        double left = random.NextDouble() * Math.Max(1f, w.Sum());
        for (int i = 0; i < among.Count; i++)
        {
            left -= w[i];
            if (left <= 0)
            {
                return among[i];
            }
        }
        return null;
    }

    /// <summary>Makes a spawned or swapped-in object beside one: turned with it, given the rule's data then its own.</summary>
    private static void Make(ICreation made, ICopy obj, SpawnPlan spawn, CustomData? ruleData, IReadOnlyDictionary<string, CustomData> entries)
    {
        var offset = Vector3.Transform(spawn.Offset, obj.Orientation);
        var at = obj.Position + offset;
        if (spawn.Snap)
        {
            at.Y = made.GroundHeight(at.X, at.Z) + offset.Y;
        }
        var copy = made.Add(spawn.Prefab, at, Vector3.Zero);
        copy.SetTurn(obj.Orientation * spawn.Turn);
        if (ruleData != null)
        {
            CustomObjectTool.Apply(ruleData, copy);
        }
        if (spawn.Data != "" && entries.GetValueOrDefault(spawn.Data) is { } own)
        {
            CustomObjectTool.Apply(own, copy);
        }
    }

    /// <summary>A draw's start from the world's seed and an object's place (to the centimetre), the same each time.</summary>
    private static int Mix(int seed, Vector3 at)
    {
        unchecked
        {
            int h = seed;
            foreach (float v in new[] { at.X, at.Y, at.Z })
            {
                h = (h * 31) ^ (int)MathF.Round(v * 100f);
                h ^= h >> 15;
                h *= 0x2c1b3c6d;
            }
            return h;
        }
    }
}

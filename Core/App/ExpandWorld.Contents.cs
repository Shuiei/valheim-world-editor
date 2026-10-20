using System.Globalization;
using TerrainEditor.Terrain;
using ValheimGen;
using NVector3 = System.Numerics.Vector3;

namespace TerrainEditor.App;

// What the location file changes about a location's objects (LocationExtraInfo), for New world to build it.
public static partial class ExpandWorld
{
    /// <summary>
    /// A location's contents as the file changes them (the mod's LocationExtraInfo): the added objects, the swaps and
    /// the data of its objects (the location's own lists over the shared ones), its own data, the scale, offset and
    /// cardinal turn; with why the game must make it instead, when the editor cannot (a blueprint, levelling or paint,
    /// commands, a time-based seed, another dungeon, random damage, data or names worked out when the game spawns it).
    /// </summary>
    /// <param name="f">The entry's fields.</param>
    /// <param name="data">The world's data entries.</param>
    /// <param name="blueprint">Whether the kind is a blueprint.</param>
    private static LocationContents ContentsOf(Fields f, DataTable data, bool blueprint)
    {
        var why = new List<string>();
        void Cannot(bool when, string what)
        {
            if (when)
            {
                why.Add(what);
            }
        }
        Cannot(blueprint, "a blueprint location");
        Cannot(f.Has("commands"), "commands");
        Cannot(f.B("randomSeed", false), "a random seed");
        Cannot(f.Str("dungeon") != "", "another dungeon");
        Cannot(f.Str("levelArea") is not ("" or "false") || f.Str("paint") != "", "levelled or painted ground");
        Cannot(f.Str("randomDamage") is "true" or "all", "random damage");
        var proxy = data.Resolve(f.Raw("data"));
        Cannot(proxy.Why != null, proxy.Why ?? "");
        var swaps = Merged(Swaps(f.Rows("objectSwap"), why), Swaps(f.Rows("locationObjectSwap"), why));
        var objectData = Merged(DataChoices(f.Rows("objectData"), data, why), DataChoices(f.Rows("locationObjectData"), data, why));
        var objects = Objects(f.Rows("objects"), data, why);
        float[] min = ScaleOf(f.Str("scaleMin", "1")), max = ScaleOf(f.Str("scaleMax", "1"));
        bool scaled = !min.SequenceEqual(max);
        return new LocationContents
        {
            Objects = objects,
            Swaps = swaps,
            Data = objectData,
            ProxyData = proxy.OrNull,
            ScaleMin = scaled ? new NVector3(min[0], min[1], min[2]) : null,
            ScaleMax = new NVector3(max[0], max[1], max[2]),
            ScaleUniform = f.B("scaleUniform", true),
            Offset = f.FOpt("offset") ?? f.F("groundOffset", 0f),
            RandomCardinal = f.B("randomCardinal", false),
            LeftToGame = why.Count > 0 ? string.Join(", ", why.Distinct()) : null,
        };
    }

    /// <summary>The shared list's choices with the location's own over them, by source.</summary>
    private static Dictionary<string, IReadOnlyList<Weighted<T>>> Merged<T>(Dictionary<string, IReadOnlyList<Weighted<T>>> shared, Dictionary<string, IReadOnlyList<Weighted<T>>> own)
    {
        foreach (var (key, list) in own)
        {
            shared[key] = list;
        }
        return shared;
    }

    /// <summary>
    /// The swaps (Create.LoadSwaps): each row "source[:weight], target[:weight]...", the targets' weights times the
    /// row's, rows of one source together, then each source's weights made to sum to 1; an empty target removes the
    /// object. A source or target that is no prefab of the game (a group, a blueprint) cannot be built.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<Weighted<string>>> Swaps(IReadOnlyList<string> rows, List<string> why) =>
        Choices(rows, why, target => target == "" || Known(target) ? (null, target) : ($"a swap to {target}", target));

    /// <summary>The data choices (Create.LoadData), as the swaps; data that cannot be written cannot be built.</summary>
    private static Dictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>> DataChoices(IReadOnlyList<string> rows, DataTable data, List<string> why) =>
        Choices(rows, why, name => data.Resolve(name) is var values ? (values.Why, values.OrNull) : default);

    /// <summary>
    /// Weighted choices by source from rows "source[:weight], choice[:weight]...", each choice made by
    /// <paramref name="make"/> (with why it cannot be built, or null); a source that is neither a prefab of the game nor
    /// "all" cannot be built.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<Weighted<T>>> Choices<T>(IReadOnlyList<string> rows, List<string> why, Func<string, (string? Why, T Value)> make)
    {
        var lists = new Dictionary<string, List<Weighted<T>>>(StringComparer.Ordinal);
        foreach (string row in rows)
        {
            string[] items = [.. row.Split(',').Select(s => s.Trim())];
            string[] source = [.. items[0].Split(':').Select(s => s.Trim()).Where(s => s != "")];
            if (source.Length == 0)
            {
                continue;
            }
            if (source[0] != "all" && !Known(source[0]))
            {
                why.Add($"a choice for {source[0]} (no prefab of the game)");
                continue;
            }
            float weight = Number(source, 1);
            if (!lists.TryGetValue(source[0], out var list))
            {
                lists[source[0]] = list = [];
            }
            foreach (string item in items.Skip(1))
            {
                string[] parts = [.. item.Split(':').Select(s => s.Trim())];
                var (cannot, made) = make(parts[0]);
                if (cannot != null)
                {
                    why.Add(cannot);
                }
                list.Add(new Weighted<T>(Number(parts, 1) * weight, made));
            }
        }
        return lists.ToDictionary(kv => kv.Key, kv =>
        {
            float total = kv.Value.Sum(w => w.Weight);
            return (IReadOnlyList<Weighted<T>>)[.. kv.Value.Select(w => w with { Weight = w.Weight / total })];
        });
    }

    /// <summary>
    /// The added objects (Helper.ParseObjects): "prefab, x, z, y, turn y, turn x, turn z, scale x, scale z, scale y,
    /// chance, data", blank fields dropped as the mod drops them, "snap" for the height to stand on the ground.
    /// </summary>
    private static List<ContentObject> Objects(IReadOnlyList<string> rows, DataTable data, List<string> why)
    {
        List<ContentObject> objects = [];
        foreach (string row in rows)
        {
            string[] s = [.. row.Split(',').Select(p => p.Trim()).Where(p => p != "")];
            if (s.Length == 0)
            {
                continue;
            }
            if (!Known(s[0]))
            {
                why.Add($"an added {s[0]} (no prefab of the game)");
                continue;
            }
            var values = data.Resolve(s.Length > 11 ? s[11] : null);
            if (values.Why != null)
            {
                why.Add(values.Why);
            }
            objects.Add(new ContentObject(s[0], new NVector3(Number(s, 1, 0f), Number(s, 3, 0f), Number(s, 2, 0f)),
                new NVector3(Number(s, 5, 0f), Number(s, 4, 0f), Number(s, 6, 0f)), new NVector3(Number(s, 7), Number(s, 9), Number(s, 8)),
                values.OrNull, Number(s, 10), s.Length > 3 && s[3].Equals("snap", StringComparison.OrdinalIgnoreCase)));
        }
        return objects;
    }

    /// <summary>A number at an index as the mod reads it (Parse.Float): the default when missing or not a number.</summary>
    private static float Number(string[] s, int index, float fallback = 1f) =>
        index < s.Length && float.TryParse(s[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

    /// <summary>Whether a name is a prefab of the game the editor can make.</summary>
    private static bool Known(string name) => PrefabCatalog.Get(Save.StableHash.Of(name)) != null;
}

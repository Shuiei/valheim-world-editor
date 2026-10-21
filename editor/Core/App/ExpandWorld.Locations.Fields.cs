using ValheimGen;

namespace TerrainEditor.App;

// The location entries' fields that need reading of their own: the flags, the biome areas and the distance rows.
public static partial class ExpandWorld
{
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
    private static List<List<DistanceRule>>? DistanceRows(Fields f, string key, List<string> notes)
    {
        List<List<DistanceRule>> rows = [];
        foreach (string item in f.Rows(key))
        {
            List<DistanceRule> row = [];
            List<string> names = [];
            foreach (string token in SplitList(item))
            {
                if (Float(token) is { } distance)
                {
                    if (names.Count == 0)
                    {
                        notes.Add($"expand_locations: {key} \"{item}\": the distance {token} has no names (left out)");
                    }
                    row.AddRange(names.Select(n => new DistanceRule(n, distance)));
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
}

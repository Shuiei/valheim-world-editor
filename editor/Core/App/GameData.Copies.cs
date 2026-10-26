using System.Text.Json;

namespace TerrainEditor.App;

// The tests' copies trimmed to the entries they hold: what each kind keeps of the game's JSON (see GameData).
public static partial class GameData
{
    /// <summary>
    /// A tests' copy made again: the game's JSON of a kind trimmed to what the copy (its JSON) holds, as the
    /// copy's text, with what the copy held and what it holds now (entries, or groups' entries).
    /// </summary>
    internal static (string Text, List<string> Kept, List<string> Now) Trim(string name, string game, string copy)
    {
        var kind = Kinds.First(k => k.Name == name);
        using var c = JsonDocument.Parse(copy);
        using var g = JsonDocument.Parse(game);
        var trimmed = JsonSerializer.SerializeToElement((kind.Trim ?? Entries)(g.RootElement, c.RootElement));
        return (Lines(trimmed) + "\n", Kept(c.RootElement, kind.Trim == null), Kept(trimmed, kind.Trim == null));
    }

    /// <summary>
    /// The game's entries the copy has.
    /// </summary>
    private static Dictionary<string, object> Entries(JsonElement game, JsonElement copy)
    {
        var kept = copy.EnumerateObject().Select(p => p.Name).ToHashSet();
        return game.EnumerateObject().Where(p => kept.Contains(p.Name)).ToDictionary(p => p.Name, p => (object)p.Value);
    }

    /// <summary>
    /// piece-cost: the pieces the copy has, and the items they take.
    /// </summary>
    private static Dictionary<string, object> TrimPieceCost(JsonElement game, JsonElement copy)
    {
        var pieces = Entries(game.GetProperty("pieces"), copy.GetProperty("pieces"));
        var items = pieces.Values.Cast<JsonElement>().SelectMany(p => p.GetProperty("r").EnumerateArray().Select(r => r[0].GetString())).ToHashSet();
        return new()
        {
            ["items"] = game.GetProperty("items").EnumerateObject().Where(p => items.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value),
            ["pieces"] = pieces,
        };
    }

    /// <summary>
    /// dungeon-rooms: every dungeon kind, and the rooms and the locations the copy has.
    /// </summary>
    private static Dictionary<string, object> TrimDungeons(JsonElement game, JsonElement copy) => new()
    {
        ["dungeons"] = game.GetProperty("dungeons"),
        ["rooms"] = Entries(game.GetProperty("rooms"), copy.GetProperty("rooms")),
        ["locations"] = copy.TryGetProperty("locations", out var locations) ? Entries(game.GetProperty("locations"), locations) : [],
    };

    /// <summary>
    /// vegetation (a list of rules): the game's rules the copy has, by name and prefab.
    /// </summary>
    private static List<JsonElement> TrimVegetation(JsonElement game, JsonElement copy)
    {
        var kept = copy.EnumerateArray().Select(RuleKey).ToHashSet();
        return [.. game.EnumerateArray().Where(r => kept.Contains(RuleKey(r)))];
    }

    /// <summary>
    /// A vegetation rule's key: "name/prefab".
    /// </summary>
    private static string RuleKey(JsonElement rule) => $"{rule.GetProperty("name").GetString()}/{rule.GetProperty("prefab").GetString()}";

    /// <summary>
    /// What a copy holds, to tell what went: its entries (a list's: its rules), else (trimmed its own
    /// way) those of each part.
    /// </summary>
    private static List<string> Kept(JsonElement copy, bool entries) =>
        copy.ValueKind == JsonValueKind.Array ? [.. copy.EnumerateArray().Select(RuleKey)]
        : [.. copy.EnumerateObject().SelectMany(p => entries ? new[] { p.Name } : p.Value.EnumerateObject().Select(e => $"{p.Name}/{e.Name}"))];
}

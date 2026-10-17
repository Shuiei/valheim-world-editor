using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Reading a report or the kept file into what is known, and writing the kept file back.
/// </summary>
public static partial class ModCatalogue
{
    /// <summary>
    /// A report or the kept file: its entries in the editor's formats (the others left out, as a broken entry
    /// would stop the game data from being used at all).
    /// </summary>
    /// <exception cref="JsonException">Not JSON, or not an object.</exception>
    private static Data Read(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("the mod catalogue is not a JSON object");
        var d = Empty();
        foreach (var (name, e) in Entries(root["prefabs"]))
        {
            if (IsPrefab(e))
            {
                d.Prefabs[name] = e.DeepClone();
            }
        }
        foreach (var (name, e) in Entries(root["pieces"]))
        {
            if (d.Prefabs.ContainsKey(name) && IsPiece(e))
            {
                d.Pieces[name] = e.DeepClone();
            }
        }
        foreach (var (name, e) in Entries(root["cost"]?["pieces"]))
        {
            if (d.Prefabs.ContainsKey(name) && IsCost(e))
            {
                d.Costs[name] = e.DeepClone();
            }
        }
        foreach (var (item, e) in Entries(root["cost"]?["items"]))
        {
            if (Text(e) is string n)
            {
                d.Items[item] = n;
            }
        }
        foreach (var (name, e) in Entries(root["mods"]))
        {
            if (Text(e) is string mod && d.Prefabs.ContainsKey(name))
            {
                d.Mods[name] = mod;
            }
        }
        foreach (var (name, e) in Entries(root["models"]))
        {
            if (d.Prefabs.ContainsKey(name) && IsModel(e))
            {
                d.Models[name] = e.DeepClone();
            }
        }
        return d;
    }

    /// <summary>The kept file's JSON.</summary>
    private static string Write(Data d)
    {
        static JsonObject Obj(IEnumerable<KeyValuePair<string, JsonNode>> e) => new(e.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.DeepClone())));
        static IEnumerable<KeyValuePair<string, JsonNode>> Texts(Dictionary<string, string> t) => t.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)JsonValue.Create(kv.Value)));
        var root = new JsonObject
        {
            ["version"] = 1,
            ["prefabs"] = Obj(d.Prefabs),
            ["pieces"] = Obj(d.Pieces),
            ["cost"] = new JsonObject { ["pieces"] = Obj(d.Costs), ["items"] = Obj(Texts(d.Items)) },
            ["mods"] = Obj(Texts(d.Mods)),
            ["models"] = Obj(d.Models),
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>An object's entries that are objects or strings; none when it is not an object.</summary>
    private static IEnumerable<(string Name, JsonNode Entry)> Entries(JsonNode? o) =>
        o is JsonObject obj ? obj.Where(kv => kv.Value != null).Select(kv => (kv.Key, kv.Value!)) : [];
}

using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Laying the mods' entries over a kind's game data: each entry the game does not have yet is added.
/// </summary>
public static partial class ModCatalogue
{
    /// <summary>
    /// A kind's game data (JSON) with the mods' entries laid over it: those the game's data does not have. A kind
    /// the mods add nothing to is returned as it is.
    /// </summary>
    /// <param name="kind">The GameData kind (prefabs, pieces, piece-cost).</param>
    /// <param name="json">The game's own data of that kind.</param>
    public static string Merge(string kind, string json)
    {
        var d = _data;
        if (!Kinds.ContainsKey(kind) || d.Prefabs.Count == 0)
        {
            return json;
        }
        var root = JsonNode.Parse(json)!.AsObject();
        if (kind == "piece-cost")
        {
            Over(root["pieces"]!.AsObject(), d.Costs);
            Over(root["items"]!.AsObject(), d.Items.ToDictionary(kv => kv.Key, kv => (JsonNode)JsonValue.Create(kv.Value)));
        }
        else
        {
            Over(root, kind == "prefabs" ? d.Prefabs : d.Pieces);
        }
        return root.ToJsonString();
    }

    /// <summary>Adds the entries an object does not have yet (copies: a node has one parent).</summary>
    private static void Over(JsonObject o, Dictionary<string, JsonNode> entries)
    {
        foreach (var (name, entry) in entries)
        {
            if (!o.ContainsKey(name))
            {
                o[name] = entry.DeepClone();
            }
        }
    }

    /// <summary>Sets an entry; whether it differs from the one there was.</summary>
    private static bool Put(Dictionary<string, JsonNode> to, string name, JsonNode entry)
    {
        if (to.TryGetValue(name, out var old) && JsonNode.DeepEquals(old, entry))
        {
            return false;
        }
        to[name] = entry;
        return true;
    }
}

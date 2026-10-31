using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// The build pieces of the mods installed on this computer, read without the game running: from the mods' own bundles
/// (ModBundles: the plugins folders the editor knows), with the same readers as the game's own pieces, and from the add-ons'
/// manifests for mods that make their pieces in code (worldeditor.json, a prefab's "piece"). GameData lays them under
/// what a live game reported (ModCatalogue), which wins, and the game's own data wins over both: they fill in the mod
/// pieces no live game has told the editor of yet. Kept in the data folder (mod-scan.json) until the mods change.
/// </summary>
public static partial class ModScan
{
    /// <summary>The kinds of GameData the mods' pieces are laid over.</summary>
    public static readonly IReadOnlyList<string> Kinds = ["prefabs", "pieces", "piece-place", "piece-support", "piece-cost"];

    /// <summary>
    /// What is known of the installed mods' pieces.
    /// </summary>
    /// <param name="Entries">By kind, each piece's entry by prefab name (that kind's format; "piece-cost": its "pieces").</param>
    /// <param name="Items">Item names by prefab name (PieceCost's "items"), for the costs' items.</param>
    /// <param name="Mods">The mod that adds each piece, by prefab name.</param>
    public sealed record Data(Dictionary<string, Dictionary<string, JsonNode>> Entries, Dictionary<string, string> Items, Dictionary<string, string> Mods)
    {
        /// <summary>Nothing known.</summary>
        public static Data Empty => new(Kinds.ToDictionary(k => k, _ => new Dictionary<string, JsonNode>(StringComparer.Ordinal)), [], []);

        /// <summary>How many pieces are known.</summary>
        public int Count => Mods.Count;
    }

    /// <summary>What is known now, swapped in whole (<see cref="Use"/>).</summary>
    private static volatile Data _data = Data.Empty;

    /// <summary>How many mod pieces are known.</summary>
    public static int Count => _data.Count;

    /// <summary>The prefab names of the mod pieces known.</summary>
    public static IEnumerable<string> Names => _data.Mods.Keys;

    /// <summary>Whether a prefab is an installed mod's piece (read here, not the game's own).</summary>
    public static bool IsModded(string prefab) => _data.Mods.ContainsKey(prefab);

    /// <summary>The mod that adds a piece, by prefab name; null when it is not one read here.</summary>
    public static string? ModOf(string prefab) => _data.Mods.GetValueOrDefault(prefab);

    /// <summary>Puts what is known in use.</summary>
    public static void Use(Data data) => _data = data;

    /// <summary>Forgets everything (the tests).</summary>
    internal static void Forget() => _data = Data.Empty;

    /// <summary>
    /// A kind's data (JSON) with the mods' pieces laid under it: those it does not have yet. A kind the mods add nothing
    /// to is returned as it is.
    /// </summary>
    /// <param name="kind">The GameData kind.</param>
    /// <param name="json">That kind's data, the game's own (and what a live game reported) already in it.</param>
    public static string Merge(string kind, string json)
    {
        var d = _data;
        if (d.Count == 0 || !d.Entries.TryGetValue(kind, out var entries) || entries.Count == 0 && kind != "piece-cost")
        {
            return json;
        }
        var root = JsonNode.Parse(json)!.AsObject();
        if (kind == "piece-cost")
        {
            Under(root["pieces"]!.AsObject(), entries);
            Under(root["items"]!.AsObject(), d.Items.ToDictionary(kv => kv.Key, kv => (JsonNode)JsonValue.Create(kv.Value)));
        }
        else
        {
            Under(root, entries);
        }
        return root.ToJsonString();
    }

    /// <summary>Adds the entries an object does not have yet (copies: a node has one parent).</summary>
    private static void Under(JsonObject o, Dictionary<string, JsonNode> entries)
    {
        foreach (var (name, entry) in entries)
        {
            if (!o.ContainsKey(name))
            {
                o[name] = entry.DeepClone();
            }
        }
    }
}

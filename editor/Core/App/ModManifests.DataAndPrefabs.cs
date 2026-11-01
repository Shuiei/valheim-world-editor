using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Reading the data and prefab notes of a manifest: each key's label, read-only flag and help, and each prefab's hidden flag, group and data flag.
/// </summary>
public static partial class ModManifests
{
    /// <summary>Adds the notes of each data key of a manifest's "data" object (those in the expected form).</summary>
    private static void ReadData(JsonNode? node, Notes notes)
    {
        if (node is not JsonObject data)
        {
            return;
        }
        foreach (var (key, e) in data)
        {
            if (e is JsonObject o && Text(o["label"], out string? label) && Flag(o["readOnly"], out bool readOnly) && Text(o["help"], out string? help))
            {
                notes.Data[key] = new DataNote(label, readOnly, help);
            }
        }
    }

    /// <summary>Adds the notes of each prefab of a manifest's "prefabs" object (those in the expected form).</summary>
    /// <param name="node">The "prefabs" object.</param>
    /// <param name="notes">Where the notes go.</param>
    /// <param name="mod">The mod whose manifest it is, when it is one's.</param>
    private static void ReadPrefabs(JsonNode? node, Notes notes, string? mod)
    {
        if (node is not JsonObject prefabs)
        {
            return;
        }
        foreach (var (name, e) in prefabs)
        {
            if (e is JsonObject o && Flag(o["hidden"], out bool hidden) && Text(o["group"], out string? group) && Flag(o["data"], out bool data))
            {
                notes.Prefabs[name] = new PrefabNote(hidden, group, data, PieceOf(o["piece"]), mod);
            }
        }
    }

    /// <summary>
    /// A prefab's "piece": its name (1 to 64 characters), its cost (an object of item prefab names to amounts from 1 to
    /// 10000, 16 at most) and its station (64 characters at most); null when there is none or it is not in that form.
    /// </summary>
    private static PieceNote? PieceOf(JsonNode? node)
    {
        if (node is not JsonObject o || !Text(o["name"], out string? name) || name is not { Length: > 0 and <= 64 }
            || !Text(o["station"], out string? station) || station is { Length: > 64 })
        {
            return null;
        }
        var cost = new List<(string, int)>();
        if (o["cost"] is JsonObject c)
        {
            foreach (var (item, amount) in c.Take(16))
            {
                if (item.Length is 0 or > 64 || amount is not JsonValue v || !v.TryGetValue(out int n) || n is < 1 or > 10000)
                {
                    return null;
                }
                cost.Add((item, n));
            }
        }
        else if (o["cost"] != null)
        {
            return null;
        }
        return new PieceNote(name, cost, station);
    }
}

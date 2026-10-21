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
    private static void ReadPrefabs(JsonNode? node, Notes notes)
    {
        if (node is not JsonObject prefabs)
        {
            return;
        }
        foreach (var (name, e) in prefabs)
        {
            if (e is JsonObject o && Flag(o["hidden"], out bool hidden) && Text(o["group"], out string? group) && Flag(o["data"], out bool data))
            {
                notes.Prefabs[name] = new PrefabNote(hidden, group, data);
            }
        }
    }
}

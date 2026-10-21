using System.Text.Json;

namespace TerrainEditor.App;

// How each kind of game data is taken into use, and how the mods' prefabs are laid over it (see GameData).
public static partial class GameData
{
    /// <summary>
    /// A kind's data in use, with the mods' prefabs laid over it when it is one of theirs (ModCatalogue.Kinds). Should
    /// the mods' entries make it fail, the game's own data is used alone.
    /// </summary>
    private static void Use(Kind kind, string json)
    {
        if (!ModCatalogue.Kinds.ContainsKey(kind.Name))
        {
            kind.Use(json);
            return;
        }
        Own[kind.Name] = json;
        string merged = ModCatalogue.Merge(kind.Name, json);
        if (ReferenceEquals(merged, json))
        {
            kind.Use(json);
            return;
        }
        try
        {
            kind.Use(merged);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or InvalidCastException)
        {
            Log.Caught(e, $"laying the mods' prefabs over the game data {kind.Name} (the game's own only)");
            kind.Use(json);
        }
    }

    /// <summary>
    /// Takes a running game's prefabs (WorldEditorBridge's GET /catalogue): those that are not the game's own are
    /// kept (ModCatalogue) and, when they change what is known, laid over the data in use. Returns how many mod
    /// prefabs the game has. Before any data is in use, they are only kept.
    /// </summary>
    /// <exception cref="JsonException">The report is not a JSON object.</exception>
    public static int AddMods(string report)
    {
        lock (Lock)
        {
            if (!Own.TryGetValue("prefabs", out string? prefabs))
            {
                return 0;
            }
            var (mods, changed) = ModCatalogue.Take(report, PrefabNames(prefabs));
            if (changed)
            {
                foreach (var kind in Kinds.Where(k => Own.ContainsKey(k.Name)))
                {
                    Use(kind, Own[kind.Name]);
                }
                _generation++;
            }
            return mods;
        }
    }

    /// <summary>The prefab names in the game's own prefabs (JSON, by name).</summary>
    private static HashSet<string> PrefabNames(string prefabs)
    {
        using var doc = JsonDocument.Parse(prefabs);
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    }
}

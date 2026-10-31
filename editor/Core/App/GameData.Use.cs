using System.Text.Json;

namespace TerrainEditor.App;

// How each kind of game data is taken into use, and how the mods' prefabs are laid over it (see GameData).
public static partial class GameData
{
    /// <summary>
    /// A kind's data in use, with the mods' prefabs laid over it when it is one of theirs: what a live game reported
    /// (ModCatalogue.Kinds), then the installed mods' pieces under that (ModScan.Kinds). Should the mods' entries make it
    /// fail, the game's own data is used alone.
    /// </summary>
    private static void Use(Kind kind, string json)
    {
        if (!ModCatalogue.Kinds.ContainsKey(kind.Name) && !ModScan.Kinds.Contains(kind.Name))
        {
            kind.Use(json);
            return;
        }
        Own[kind.Name] = json;
        string merged = ModScan.Merge(kind.Name, ModCatalogue.Merge(kind.Name, json));
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

    /// <summary>
    /// Reads the installed mods' pieces (ModScan: their bundles, kept until they change, and the add-ons' manifests) and
    /// lays them under the data in use. Returns how many mod pieces there are; 0, read nothing, before the game's data is
    /// in use.
    /// </summary>
    /// <param name="settings">The editor's settings (the plugins folders it knows).</param>
    /// <param name="again">Read the mods' bundles even when the kept reading is of the same mods.</param>
    /// <param name="progress">Told the bundles read, of all of them, while it reads them.</param>
    public static int ScanMods(AppSettings settings, bool again = false, Action<int, int>? progress = null)
    {
        string? from;
        string? prefabs;
        lock (Lock)
        {
            from = _from;
            prefabs = Own.GetValueOrDefault("prefabs");
        }
        if (from == null || from == Copies || prefabs == null)
        {
            return 0;
        }
        // Read outside the lock: reading the bundles takes a while, and the editor goes on meanwhile.
        var data = ModScan.Scan(settings, from, PrefabNames(prefabs), again, progress);
        UseModScan(data);
        return data.Count;
    }

    /// <summary>The installed mods' pieces in use (ModScan), laid under the data in use.</summary>
    internal static void UseModScan(ModScan.Data data)
    {
        lock (Lock)
        {
            ModScan.Use(data);
            foreach (var kind in Kinds.Where(k => Own.ContainsKey(k.Name)))
            {
                Use(kind, Own[kind.Name]);
            }
            _generation++;
        }
    }
}

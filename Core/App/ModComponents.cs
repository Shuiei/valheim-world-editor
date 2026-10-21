using System.Text.Json;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The fields of the game's scripts that mods' extensions ask for (their worldeditor.json's "gameComponents";
/// IEditorHost.Game): read from the player's Valheim in one pass over its bundles as a world or the Workshop opens, for
/// the running extensions only, and kept in the data folder (game-mod-components.json) until the game, the editor or
/// the classes asked for change.
/// </summary>
public static class ModComponents
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<GameComponent>> None = new Dictionary<string, IReadOnlyList<GameComponent>>();

    /// <summary>The components read, by script class, swapped in whole.</summary>
    private static volatile IReadOnlyDictionary<string, IReadOnlyList<GameComponent>> _byClass = None;

    /// <summary>What the extensions are given (IEditorHost.Game).</summary>
    public static IGameData View { get; } = new GameView();

    /// <summary>The components of a script class read last; empty for one not asked for.</summary>
    /// <param name="scriptClass">The script's class name.</param>
    public static IReadOnlyList<GameComponent> Of(string scriptClass) => _byClass.TryGetValue(scriptClass, out var list) ? list : [];

    /// <summary>
    /// Reads the components of the classes asked for (from the cache when the game, the editor and the classes are the
    /// same); none without a game or classes. Never throws: a game whose files cannot be read gives none (logged).
    /// </summary>
    /// <param name="bundles">The game's bundles; null when the game is not found.</param>
    /// <param name="classes">The script classes the running extensions ask for.</param>
    public static void Refresh(GameBundles? bundles, IReadOnlyCollection<string> classes)
    {
        if (bundles == null || classes.Count == 0)
        {
            _byClass = None;
            return;
        }
        try
        {
            string asked = string.Join(",", classes.Distinct().Order(StringComparer.Ordinal));
            string stamp = "mod-components " + asked + " " + GameData.Stamp(GameData.ValheimOf(bundles.Folder), bundles.Folder);
            string file = GameData.CacheFile("mod-components");
            string? json = GameData.Cached(file, stamp);
            if (json == null)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                json = GameCatalogues.ReadComponents(bundles, [.. asked.Split(',')]);
                Console.WriteLine($"Game components for mods' extensions ({asked}): read from the game's bundles in {watch.ElapsedMilliseconds} ms.");
                GameData.KeepCache(file, stamp + "\n" + json);
            }
            _byClass = Parse(json);
        }
        catch (Exception e)
        {
            // Anything in the game's files can fail the reading: the extensions get none.
            Log.Caught(e, "reading the game's components for mods' extensions (they get none)");
            _byClass = None;
        }
    }

    /// <summary>The components by class, from the JSON GameCatalogues.ReadComponents writes.</summary>
    /// <param name="json">The JSON.</param>
    internal static Dictionary<string, IReadOnlyList<GameComponent>> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var byClass = new Dictionary<string, IReadOnlyList<GameComponent>>(StringComparer.Ordinal);
        foreach (var c in doc.RootElement.EnumerateObject())
        {
            byClass[c.Name] = [.. c.Value.EnumerateArray().Select(e => new GameComponent(
                e.GetProperty("prefab").GetString() ?? "",
                e.GetProperty("object").GetString() ?? "",
                (IReadOnlyDictionary<string, object?>)Value(e.GetProperty("fields"))!))];
        }
        return byClass;
    }

    /// <summary>A JSON value as the SDK hands fields over: long, double, string, list or map.</summary>
    private static object? Value(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.TryGetInt64(out long l) ? l : e.GetDouble(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.True => 1L,
        JsonValueKind.False => 0L,
        JsonValueKind.Array => (IReadOnlyList<object?>)[.. e.EnumerateArray().Select(Value)],
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => Value(p.Value), StringComparer.Ordinal),
        _ => null,
    };

    /// <summary>What an extension reads the components through.</summary>
    private sealed class GameView : IGameData
    {
        /// <inheritdoc/>
        public IReadOnlyList<GameComponent> Components(string scriptClass) => Of(scriptClass);
    }
}

using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// What the world's locations are called by players, by the game's location name: the start, traders and bosses'
    /// altars (SeedPreview.Landmarks), and the dungeons' entrances (SeedPreview.Dungeons).
    /// </summary>
    internal static readonly Dictionary<string, (string What, string Kind)> Known = BuildKnown();

    /// <summary>Builds <see cref="Known"/>.</summary>
    private static Dictionary<string, (string What, string Kind)> BuildKnown()
    {
        var known = new Dictionary<string, (string What, string Kind)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (prefab, name, kind) in SeedPreview.Landmarks)
        {
            known[prefab] = (name, kind);
        }
        foreach (var (name, _, prefabs) in SeedPreview.Dungeons)
        {
            foreach (string p in prefabs)
            {
                known.TryAdd(p, (name, "dungeon"));
            }
        }
        return known;
    }

    /// <summary>The known locations' game names by hash (<see cref="Known"/>), named even without the game's rules.</summary>
    private static readonly Dictionary<int, string> KnownByHash = Known.Keys.ToDictionary(Save.StableHash.Of, k => k);

    /// <summary>
    /// The game's location names by hash: the location rules read from the game, then the known locations, then the
    /// prefab catalogue.
    /// </summary>
    internal static string? LocationName(int hash, Dictionary<int, string> rules) =>
        rules.TryGetValue(hash, out string? n) ? n : KnownByHash.TryGetValue(hash, out n) ? n : Terrain.PrefabCatalog.NameOf(hash);

    /// <summary>The game's location rules in use (read from the game once, as the New world page does).</summary>
    private static Task GameRules() => GameLocations.UseGameRules(GameLook.Bundles);
}

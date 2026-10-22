using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

/// <summary>
/// Names of object data keys (the save keeps only their hashes), from the player's game code
/// (GameData; the tests use editor/tests/fixtures/zdo-keys.json): for the object inspector.
/// </summary>
public static class ZdoKeys
{
    /// <summary>
    /// None until Use: no world opens before then (GameData).
    /// </summary>
    private static volatile Dictionary<int, string> _byHash = [];

    /// <summary>
    /// Names from the player's mods' code (ModKeys), for the keys the game's code has no name for.
    /// </summary>
    private static volatile IReadOnlyDictionary<int, string> _mods = new Dictionary<int, string>();

    /// <summary>
    /// Takes the names found in the mods' code, by hash (the game's own names still come first).
    /// </summary>
    public static void UseMods(IReadOnlyDictionary<int, string> names) => _mods = names;

    /// <summary>
    /// Takes the names by hash, as GameCatalogues.ZdoKeysJson writes them (hash keys as decimal text).
    /// </summary>
    public static void Use(string json) =>
        _byHash = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!.ToDictionary(kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture), kv => kv.Value);

    /// <summary>
    /// The name a key hash stands for: the game's code's, else a mod's (its DLL's text, else its manifest's "data"
    /// keys); null when none has a key with that hash.
    /// </summary>
    public static string? NameOf(int key) => _byHash.TryGetValue(key, out string? n) ? n : _mods.GetValueOrDefault(key) ?? App.ModManifests.KeyNameOf(key);

    /// <summary>
    /// A key given by name or as a number ("text", "-1234"): a number is taken as the hash itself, a name is hashed.
    /// </summary>
    public static int Parse(string key) => int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int k) ? k : StableHash.Of(key);
}

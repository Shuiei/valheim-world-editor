using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

/// <summary>
/// Names of object data keys (the save keeps only their hashes), from the player's game code
/// (GameData; the tests use tests/fixtures/zdo-keys.json): for the object inspector.
/// </summary>
public static class ZdoKeys
{
    /// <summary>
    /// None until Use: no world opens before then (GameData).
    /// </summary>
    private static volatile Dictionary<int, string> _byHash = [];

    /// <summary>
    /// Takes the names by hash, as GameCatalogues.ZdoKeysJson writes them (hash keys as decimal text).
    /// </summary>
    public static void Use(string json) =>
        _byHash = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!.ToDictionary(kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture), kv => kv.Value);

    /// <summary>
    /// The name a key hash stands for; null when the game code has no key with that hash.
    /// </summary>
    public static string? NameOf(int key) => _byHash.TryGetValue(key, out string? n) ? n : null;

    /// <summary>
    /// A key given by name or as a number ("text", "-1234"): a number is taken as the hash itself, a name is hashed.
    /// </summary>
    public static int Parse(string key) => int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int k) ? k : StableHash.Of(key);
}

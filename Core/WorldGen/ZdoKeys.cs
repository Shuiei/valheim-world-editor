using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

// Names of object data keys (the save keeps only their hashes), from the player's game code
// (GameData; the tests use tests/fixtures/zdo-keys.json): for the object inspector.
public static class ZdoKeys
{
    // None until Use: no world opens before then (GameData).
    private static volatile Dictionary<int, string> _byHash = [];

    // The names by hash, as GameCatalogues.ZdoKeysJson writes them.
    public static void Use(string json) =>
        _byHash = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!.ToDictionary(kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture), kv => kv.Value);

    public static string? NameOf(int key) => _byHash.TryGetValue(key, out string? n) ? n : null;

    // A key given by name or as a number ("text", "-1234").
    public static int Parse(string key) => int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int k) ? k : StableHash.Of(key);
}

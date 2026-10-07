using System.Reflection;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

// Names of object data keys (the save keeps only their hashes), from the game code
// (tools/asset-export/scan_zdo_keys.py): for the object inspector.
public static class ZdoKeys
{
	private static readonly Dictionary<int, string> ByHash = Load();

	public static string? NameOf(int key) => ByHash.TryGetValue(key, out string? n) ? n : null;

	// A key given by name or as a number ("text", "-1234").
	public static int Parse(string key) => int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int k) ? k : StableHash.Of(key);

	private static Dictionary<int, string> Load()
	{
		using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerrainEditor.zdo-keys.json")
			?? throw new InvalidOperationException("zdo-keys.json is not embedded");
		return JsonSerializer.Deserialize<Dictionary<string, string>>(s)!.ToDictionary(kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture), kv => kv.Value);
	}
}

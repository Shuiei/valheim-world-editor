using System.Text.Json;

namespace TerrainEditor.Terrain;

// What build pieces cost in game, for the blueprint library: each piece's English name, its resources
// (item and amount, as the hammer takes them) and the crafting station that must be near to build it
// (piece-cost.json, tools/asset-export/scan_piece_cost.py, from the game's Piece components).
public static class PieceCost
{
	public sealed record Info(string Name, string? Station, (string Item, int Amount)[] Resources);

	private static readonly Lazy<(Dictionary<string, Info> Pieces, Dictionary<string, string> Items)> Data = new(Load);

	private static (Dictionary<string, Info>, Dictionary<string, string>) Load()
	{
		using Stream s = typeof(PieceCost).Assembly.GetManifestResourceStream("TerrainEditor.piece-cost.json")
			?? throw new InvalidOperationException("piece-cost.json is not embedded");
		using JsonDocument doc = JsonDocument.Parse(s);
		var pieces = new Dictionary<string, Info>();
		foreach (JsonProperty p in doc.RootElement.GetProperty("pieces").EnumerateObject())
		{
			var res = p.Value.GetProperty("r").EnumerateArray().Select(r => (r[0].GetString()!, r[1].GetInt32())).ToArray();
			pieces[p.Name] = new Info(p.Value.GetProperty("n").GetString()!, p.Value.TryGetProperty("st", out var st) ? st.GetString() : null, res);
		}
		var items = doc.RootElement.GetProperty("items").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
		return (pieces, items);
	}

	public static Info? Get(string prefab) => Data.Value.Pieces.GetValueOrDefault(prefab);

	// An item's English name (Wood, Fine wood, Bronze nails), else its prefab name.
	public static string ItemName(string item) => Data.Value.Items.GetValueOrDefault(item, item);

	// A piece's English name, else its prefab name.
	public static string PieceName(string prefab) => Get(prefab)?.Name ?? prefab;

	// What a building costs: materials (most first), the stations it needs, and the kinds of piece the
	// game has no recipe for (from mods, or not buildable) with how many there are.
	public sealed record Total(List<(string Item, string Name, int Amount)> Materials, List<string> Stations, Dictionary<string, int> Unknown)
	{
		// "Wood 24 · Stone 8 · Resin 2", the first few (… when there are more), and its stations.
		public string Describe(int most = 4)
		{
			if (Materials.Count == 0)
			{
				return Unknown.Count > 0 ? "Cost unknown (pieces from mods?)" : "Free";
			}
			string text = string.Join(" · ", Materials.Take(most).Select(m => $"{m.Name} {m.Amount}")) + (Materials.Count > most ? " · …" : "");
			return Stations.Count > 0 ? $"{text} (needs {string.Join(", ", Stations)})" : text;
		}
	}

	// The cost of so many of each piece kind (prefab name → count).
	public static Total Of(IEnumerable<KeyValuePair<string, int>> kinds)
	{
		var amounts = new Dictionary<string, int>();
		var stations = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var unknown = new Dictionary<string, int>();
		foreach (var (prefab, count) in kinds)
		{
			if (Get(prefab) is not { } info)
			{
				unknown[prefab] = unknown.GetValueOrDefault(prefab) + count;
				continue;
			}
			if (info.Station is { Length: > 0 } st)
			{
				stations.Add(st);
			}
			foreach (var (item, amount) in info.Resources)
			{
				amounts[item] = amounts.GetValueOrDefault(item) + amount * count;
			}
		}
		var materials = amounts.Select(a => (a.Key, ItemName(a.Key), a.Value)).OrderByDescending(m => m.Value).ThenBy(m => m.Item2, StringComparer.OrdinalIgnoreCase).ToList();
		return new Total(materials, stations.ToList(), unknown);
	}

	public static Total Of(IEnumerable<string> prefabs) => Of(prefabs.GroupBy(p => p).Select(g => KeyValuePair.Create(g.Key, g.Count())));
}

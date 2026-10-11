using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

// Build pieces (prefabs with a Piece component), read from the player's own Valheim (GameData; the
// tests use tests/fixtures/pieces.json): category and footprint (bounds of the non-trigger box
// colliders in the piece's own frame), so player-built structures can be drawn on the map, and snap
// points (where the hammer snaps pieces together).
public static class PieceCatalog
{
	public sealed record Info(string Name, int Index, int Category, float MinX, float MaxX, float MinZ, float MaxZ, float MinY, float MaxY)
	{
		// Snap points in the piece's own frame (x, y, z each); empty when it has none.
		public float[][] Snaps { get; init; } = Array.Empty<float[]>();

		// The build tool whose menu has the piece (hammer, hoe, cultivator, feaster: the serving tray);
		// null for pieces players cannot build (parts of ruins and dungeons).
		public string? Tool { get; init; }
	}

	// None until Use: no world or Workshop opens before then (GameData).
	private static volatile Data Catalogue = new(new(), Array.Empty<string>(), new());

	private sealed record Data(Dictionary<int, Info> ByHash, string[] Names, HashSet<string> Known);

	// Every piece with snap points, by name.
	public static IEnumerable<Info> WithSnaps => Catalogue.ByHash.Values.Where(i => i.Snaps.Length > 0);

	// Prefab names in catalogue order; Info.Index points into this list.
	public static string[] Names => Catalogue.Names;

	public static int Count => Catalogue.ByHash.Count;

	public static Info? Get(int prefab) => Catalogue.ByHash.TryGetValue(prefab, out var info) ? info : null;

	// Whether a prefab (by name) is a piece.
	public static bool Has(string name) => Catalogue.Known.Contains(name);

	// Per piece: x, y, z, rotation Y (degrees), footprint minX, maxX, minZ, maxZ, top (y), category, bottom (y),
	// prefab (index into Names), object id.
	public const int Stride = 13;

	public static float[] Encode(WorldSave world, ICollection<int> deleted)
	{
		List<float> data = new(world.Pieces.Count * Stride);
		List<(int Id, int Prefab, System.Numerics.Vector3 Position, float RotationY)> pieces;
		lock (world.Sync)
		{
			pieces = world.Pieces.ToList();
		}
		foreach (var (id, prefab, pos, rotY) in pieces)
		{
			if (deleted.Contains(id))
			{
				continue;
			}
			Info? info = Get(prefab);
			if (info == null)
			{
				continue;
			}
			data.AddRange(new[] { pos.X, pos.Y, pos.Z, rotY, info.MinX, info.MaxX, info.MinZ, info.MaxZ, pos.Y + info.MaxY, info.Category, pos.Y + info.MinY, info.Index, id });
		}
		return data.ToArray();
	}

	// The pieces by name, as GameCatalogues' PieceScan writes them.
	public static void Use(string json)
	{
		using JsonDocument doc = JsonDocument.Parse(json);
		Dictionary<int, Info> result = new();
		List<string> names = new();
		foreach (JsonProperty p in doc.RootElement.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
		{
			int cat = p.Value.GetProperty("cat").GetInt32();
			// Pieces without box colliders get a 1 m marker.
			float[] box = p.Value.TryGetProperty("box", out JsonElement b) ? b.EnumerateArray().Select(e => e.GetSingle()).ToArray() : new[] { -0.5f, 0.5f, -0.5f, 0.5f, 0f, 1f };
			float[][] snaps = p.Value.TryGetProperty("snap", out JsonElement sp) ? sp.EnumerateArray().Select(v => v.EnumerateArray().Select(e => e.GetSingle()).ToArray()).ToArray() : Array.Empty<float[]>();
			string? tool = p.Value.TryGetProperty("tool", out JsonElement t) ? t.GetString() : null;
			result[StableHash.Of(p.Name)] = new Info(p.Name, names.Count, cat, box[0], box[1], box[2], box[3], box[4], box[5]) { Snaps = snaps, Tool = tool };
			names.Add(p.Name);
		}
		Catalogue = new Data(result, names.ToArray(), names.ToHashSet(StringComparer.Ordinal));
	}
}

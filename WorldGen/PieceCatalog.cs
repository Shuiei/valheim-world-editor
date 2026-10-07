using System.Reflection;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

// Build pieces (prefabs with a Piece component) extracted from the game's asset bundles: category
// and footprint (bounds of the non-trigger box colliders in the piece's own frame), so player-built
// structures can be drawn on the map, and snap points (where the hammer snaps pieces together).
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

	// Every piece with snap points, by name.
	public static IEnumerable<Info> WithSnaps => ByHash.Values.Where(i => i.Snaps.Length > 0);

	private static readonly Dictionary<int, Info> ByHash = Load();

	public static int Count => ByHash.Count;

	// Prefab names in catalogue order; Info.Index points into this list.
	// Set by Load (no initializer: it would run after ByHash and overwrite it).
	public static string[] Names { get; private set; }

	public static Info? Get(int prefab) => ByHash.TryGetValue(prefab, out var info) ? info : null;

	// Per piece: x, y, z, rotation Y (degrees), footprint minX, maxX, minZ, maxZ, top (y), category, bottom (y),
	// prefab (index into Names), object id.
	public const int Stride = 13;

	public static float[] Encode(WorldSave world, ICollection<int> deleted)
	{
		List<float> data = new(world.Pieces.Count * Stride);
		foreach (var (id, prefab, pos, rotY) in world.Pieces)
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

	private static Dictionary<int, Info> Load()
	{
		using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerrainEditor.pieces.json")
			?? throw new InvalidOperationException("pieces.json is not embedded");
		using JsonDocument doc = JsonDocument.Parse(s);
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
		Names = names.ToArray();
		return result;
	}
}

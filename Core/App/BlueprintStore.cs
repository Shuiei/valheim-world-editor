using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

// Saved copies ("blueprints", like WorldEdit's schematics): what the Area or Select tool copied, kept as
// one JSON file each in the app's data folder (blueprints/), so they can be pasted into any world.
// A file holds the name, where it came from, a small picture and the copy itself in the editor's
// clipboard format (ground shape, paint and objects relative to the copy's centre).
public sealed class BlueprintStore(string directory)
{
	public string Directory { get; } = directory;

	public static BlueprintStore Default => new(Path.Combine(AppSettings.DataDir, "blueprints"));

	public sealed record Summary(string Id, string Name, DateTime Created, string? World, int W, int H, int Objects, bool Ground, string? Thumb, string? Source);

	public List<Summary> List()
	{
		if (!System.IO.Directory.Exists(Directory))
		{
			return new();
		}
		List<Summary> list = new();
		foreach (string file in System.IO.Directory.GetFiles(Directory, "*.json"))
		{
			try
			{
				JsonNode? doc = JsonNode.Parse(File.ReadAllText(file));
				if (doc?["clip"] is not JsonObject clip)
				{
					continue;
				}
				list.Add(new Summary(Path.GetFileNameWithoutExtension(file), (string?)doc["name"] ?? Path.GetFileNameWithoutExtension(file),
					doc["created"] is JsonNode c && DateTime.TryParse((string?)c, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t) ? t : File.GetLastWriteTime(file),
					(string?)doc["world"], (int?)clip["w"] ?? 0, (int?)clip["h"] ?? 0, (clip["objects"] as JsonArray)?.Count ?? 0, HasGround(clip), (string?)doc["thumb"], (string?)doc["source"]));
			}
			catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or FormatException)
			{
				// Not a blueprint (or a half-written file): left out of the list.
			}
		}
		return list.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	// The whole file, or null when there is no such blueprint.
	public string? Read(string id) => ValidId(id) && File.Exists(PathOf(id)) ? File.ReadAllText(PathOf(id)) : null;

	// Saves a blueprint under a file name made from its name; a blueprint with the same name is replaced.
	public string Save(string name, string? world, string? thumb, JsonObject clip, string? source = null)
	{
		name = name.Trim();
		if (name.Length == 0)
		{
			throw new ArgumentException("A blueprint needs a name.");
		}
		if (clip["objects"] is not JsonArray || clip["w"] == null || clip["h"] == null)
		{
			throw new ArgumentException("That is not a copy from the editor.");
		}
		System.IO.Directory.CreateDirectory(Directory);
		string id = IdFor(name);
		JsonObject doc = new()
		{
			["name"] = name,
			["created"] = DateTime.Now.ToString("o"),
			["world"] = world,
			["source"] = source,
			["thumb"] = thumb,
			["clip"] = clip.DeepClone(),
		};
		string tmp = PathOf(id) + ".tmp";
		File.WriteAllText(tmp, doc.ToJsonString());
		File.Move(tmp, PathOf(id), overwrite: true);
		return id;
	}

	public bool Delete(string id)
	{
		if (!ValidId(id) || !File.Exists(PathOf(id)))
		{
			return false;
		}
		File.Delete(PathOf(id));
		return true;
	}

	public bool Exists(string name) => File.Exists(PathOf(IdFor(name.Trim())));

	// A file name from the blueprint's name: letters, digits, spaces, dashes and dots are kept.
	public static string IdFor(string name)
	{
		string id = new(name.Trim().Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' ? c : '_').ToArray());
		id = id.Trim('.', ' ');
		if (id.Length == 0)
		{
			id = "blueprint";
		}
		return id.Length > 80 ? id[..80] : id;
	}

	private static bool ValidId(string id) => id.Length > 0 && id == IdFor(id) && Path.GetFileName(id) == id;

	private string PathOf(string id) => Path.Combine(Directory, id + ".json");

	// The copy has ground when any point of its relative heights is set (-32768 marks "no ground").
	private static bool HasGround(JsonObject clip) => clip["rel"] is JsonArray rel && rel.Any(v => v != null && (int)v != -32768);
}

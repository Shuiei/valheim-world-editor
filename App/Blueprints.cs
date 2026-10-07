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

public static class BlueprintEndpoints
{
	public static void Map(WebApplication app, Func<string> worldName, Func<string, bool> canCreate)
	{
		app.MapGet("/api/blueprints", () => new { folder = BlueprintStore.Default.Directory, list = BlueprintStore.Default.List() });
		app.MapGet("/api/blueprints/{id}", (string id) => BlueprintStore.Default.Read(id) is string json ? Results.Content(json, "application/json") : Results.NotFound());
		app.MapPost("/api/blueprints", (BlueprintSave req) =>
		{
			try
			{
				string id = BlueprintStore.Default.Save(req.Name, worldName(), req.Thumb, req.Clip, req.Source);
				return Results.Ok(new { id });
			}
			catch (ArgumentException ex)
			{
				return Results.BadRequest(ex.Message);
			}
		});
		app.MapGet("/api/blueprints/exists", (string name) => new { exists = BlueprintStore.Default.Exists(name) });
		app.MapDelete("/api/blueprints/{id}", (string id) => BlueprintStore.Default.Delete(id) ? Results.Ok(new { deleted = id }) : Results.NotFound());

		// A PlanBuild .blueprint or .vbuild file (its text, or a path chosen in the app window) as a clipboard.
		app.MapPost("/api/blueprints/import", (BlueprintImport req) =>
		{
			string? text = req.Text;
			string fileName = req.FileName ?? (req.Path != null ? Path.GetFileName(req.Path) : "imported.blueprint");
			if (text == null && req.Path != null)
			{
				if (!File.Exists(req.Path) || new FileInfo(req.Path).Length > 32 << 20)
				{
					return Results.BadRequest("That file cannot be read.");
				}
				text = File.ReadAllText(req.Path);
			}
			if (text == null)
			{
				return Results.BadRequest("No file given.");
			}
			BlueprintFormats.Parsed parsed = BlueprintFormats.Parse(fileName, text);
			if (parsed.Pieces.Count == 0 && parsed.Terrain.Count == 0)
			{
				return Results.BadRequest("No pieces found in that file. Is it a PlanBuild .blueprint or a .vbuild file?");
			}
			JsonObject clip = BlueprintFormats.ToClip(parsed, canCreate, out List<string> unknown);
			return Results.Ok(new { name = parsed.Name, creator = parsed.Creator, clip, unknown, pieces = parsed.Pieces.Count, terrain = parsed.Terrain.Count, skipped = parsed.SkippedLines });
		});

		// Writes a blueprint as a PlanBuild .blueprint or a .vbuild file into blueprints/export.
		app.MapPost("/api/blueprints/{id}/export", (string id, BlueprintExport req) =>
		{
			if (BlueprintStore.Default.Read(id) is not string json || JsonNode.Parse(json) is not JsonObject doc || doc["clip"] is not JsonObject clip)
			{
				return Results.NotFound();
			}
			string format = req.Format == "vbuild" ? "vbuild" : "blueprint";
			string name = (string?)doc["name"] ?? id;
			string text = BlueprintFormats.Write(clip, format, name, n => TerrainEditor.Terrain.PieceCatalog.Get(TerrainEditor.Save.StableHash.Of(n))?.Category ?? 0);
			string dir = Path.Combine(BlueprintStore.Default.Directory, "export");
			System.IO.Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, id + "." + format);
			File.WriteAllText(path, text);
			return Results.Ok(new { path, text, fileName = id + "." + format });
		});
	}
}

public sealed record BlueprintImport(string? FileName, string? Text, string? Path);

public sealed record BlueprintExport(string? Format);

public sealed record BlueprintSave(string Name, string? Thumb, JsonObject Clip, string? Source);

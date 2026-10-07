using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

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

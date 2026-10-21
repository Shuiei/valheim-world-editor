using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>
    /// Other mods' blueprint files (PlanBuild .blueprint, .vbuild, Homestead's from elsewhere): read, then
    /// kept as a Homestead blueprint.
    /// </summary>
    /// <returns>The name it was saved under (numbered when taken), null when not imported.</returns>
    public string? Import(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading a blueprint file to import");
            Message?.Invoke(Said.Of("blueprints.msg.importFailed", ex.Message));
            return null;
        }
        var parsed = BlueprintFormats.Parse(Path.GetFileName(path), text);
        if (parsed.Pieces.Count == 0 && parsed.Terrain.Count == 0)
        {
            Message?.Invoke(Said.Of("blueprints.msg.noPieces"));
            return null;
        }
        Status = FindHomestead();
        var world = _scene()?.World;
        JsonObject clip;
        List<string> unknown;
        try
        {
            clip = BlueprintFormats.ToClip(parsed, n => world?.CanCreate(StableHash.OfName(n)) ?? Terrain.PrefabCatalog.Get(StableHash.OfName(n)) != null, out unknown);
        }
        catch (InvalidDataException ex)
        {
            Log.CaughtBrief(ex, "turning an imported blueprint into a copy");
            Message?.Invoke(Said.Of("blueprints.msg.importInvalid", ex.Message));
            return null;
        }
        string name = Homestead.FreeName(string.IsNullOrWhiteSpace(parsed.Name) ? Lang.T("blueprints.importedName") : parsed.Name, HomesteadExists);
        if (!TryWrite(() => WriteHomestead(clip, new Homestead.Details(name, parsed.Description ?? "", Homestead.Details.ParseTags(TagsOf(text))), null)))
        {
            return null;
        }
        Refresh();
        int count = (clip["objects"] as JsonArray)?.Count ?? 0;
        Message?.Invoke(ImportedSaid(name, count, parsed.Terrain.Count > 0, unknown, parsed.SkippedLines));
        return name;
    }
}

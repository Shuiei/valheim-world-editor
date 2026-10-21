using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's blueprints (open_workshop, save_blueprint, list_blueprints): the library's files and the Workshop's building.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>open_workshop: the blank plot, optionally with a library blueprint; refused while there are unsaved changes.</summary>
    [McpServerTool(Name = "open_workshop", Title = "Open the Workshop")]
    [Description("Opens the Workshop: a blank 192 m plot (ground at y = 34, its middle at x = 0, z = 0) to build a blueprint on, optionally with a blueprint of the library on it (list_blueprints): this is how a blueprint is loaded. Refused while a world or the Workshop has changes not saved (save_blueprint saves the Workshop's building).")]
    public Task<string> OpenWorkshop([Description("A blueprint's name from list_blueprints (optional: an empty plot).")] string? blueprint = null) =>
        WithLibraryFile(blueprint, found => OnUi(async () =>
    {
        if (Unsaved() is string why)
        {
            return why;
        }
        string? path = null;
        if (!string.IsNullOrWhiteSpace(blueprint))
        {
            path = found;
            if (path == null)
            {
                return $"No blueprint \"{blueprint}\" in the library (list_blueprints).";
            }
        }
        await _w.OpenWorkshop(path);
        return ToJson(StateOf(_w));
    }));

    /// <summary>
    /// The library file of a blueprint named as list_blueprints names it (its #Name: header, or its file's name when
    /// it has none), whatever its file is called (the game's own files, downloaded ones and numbered copies are not
    /// named after their header): of the entries with that name, the one in the file named after it when there is
    /// one (as the editor saves them: "Tower" is Tower.blueprint, not "Tower (2)"), else the first. With no entry
    /// of that name, a file named after it. Null for none.
    /// </summary>
    /// <param name="folder">The library's folder.</param>
    /// <param name="name">The blueprint's name.</param>
    internal static string? BlueprintPath(string folder, string name)
    {
        string file = Homestead.FileName(name);
        var entries = Homestead.List(folder).Where(e => e.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if ((entries.Find(e => string.Equals(Path.GetFileName(e.Path), file, StringComparison.OrdinalIgnoreCase)) ?? entries.FirstOrDefault()) is { } entry)
        {
            return entry.Path;
        }
        string path = Path.Combine(folder, file);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Finds a library blueprint's file (<see cref="BlueprintPath"/>) away from the window's thread, as it reads every
    /// blueprint of the library, then answers with <paramref name="then"/> given it (null: none, or no name given).
    /// </summary>
    /// <param name="name">The blueprint's name, or null or blank for none.</param>
    /// <param name="then">The tool's work with the file found.</param>
    private async Task<string> WithLibraryFile(string? name, Func<string?, Task<string>> then)
    {
        // Homestead looked for again: installed or moved since the panel last looked.
        string folder = await OnUi(() => _w.Blueprints.LookAgain().Folder);
        string? path = string.IsNullOrWhiteSpace(name) ? null : await Task.Run(() => BlueprintPath(folder, name));
        return await then(path);
    }

    /// <summary>save_blueprint: the Workshop's building saved as a blueprint of the library (MainWindow.SaveWorkshopAs).</summary>
    [McpServerTool(Name = "save_blueprint", Title = "Save the Workshop's building as a blueprint")]
    [Description("Saves the building on the Workshop's plot as a blueprint of the user's library (Homestead's), as the Workshop's Save blueprint does: its name, a description and tags, with what its objects hold (chest contents, stands' items) and a picture. After it, the Workshop holds nothing unsaved, so another blueprint can be opened (open_workshop). Refused when pieces would fall in game (support_check; evenIfFalling saves anyway) and when another blueprint of that name exists: replacing one (overwrite) is the user's call, ask first.")]
    public Task<string> SaveBlueprint(
        [Description("The blueprint's name (also its file's).")] string name,
        [Description("A few words about it, shown in the library (optional).")] string description = "",
        [Description("Words to find it by (optional), e.g. [\"house\", \"log\"].")] string[]? tags = null,
        [Description("Replace a blueprint of that name (only when the user said so; default false).")] bool overwrite = false,
        [Description("Save even if pieces would fall in game (default false).")] bool evenIfFalling = false) => OnUi(() =>
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Give the blueprint a name.";
        }
        var details = new Homestead.Details(name.Trim(), description.Trim(), [.. (tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0)]);
        return _w.SaveWorkshopAs(details, overwrite, evenIfFalling) is string problem ? problem : _w.LastMessageEn + " " + ToJson(StateOf(_w));
    });

    /// <summary>list_blueprints: Homestead's library, filtered by words of the name, creator, description or tags.</summary>
    [McpServerTool(Name = "list_blueprints", ReadOnly = true, Title = "The blueprint library")]
    [Description("The user's blueprints (Homestead's library): name, pieces, cost (what Homestead's hammer takes: pieces only the editor can place, such as statues, ships and creatures, cost nothing), how many pieces only the editor can place (editorOnly; null when the file does not say), description, tags. Open one in the Workshop (open_workshop) or add it to the plot (add_blueprint).")]
    public Task<string> ListBlueprints([Description("Words to find in the name, creator, description or tags (optional).")] string? query = null) => OnUi(() =>
    {
        return ToJson(Homestead.List(_w.Blueprints.LookAgain().Folder)
            .Where(e => e.Matches(query))
            .Select(e => new { name = e.Name, pieces = e.Pieces, cost = Homestead.Cost(e.Kinds).Describe(4, english: true), editorOnly = e.EditorOnlyNote?.English, description = e.Description, tags = e.Tags }));
    });
}

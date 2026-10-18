namespace TerrainEditor.App;

/// <summary>
/// Saved copies ("blueprints", like WorldEdit's schematics): what the Area or Select tool copied, kept as
/// one JSON file each in the app's data folder (blueprints/), so they can be pasted into any world.
/// A file holds the name, where it came from, a small picture and the copy itself in the editor's
/// clipboard format (ground shape, paint and objects relative to the copy's centre).
/// </summary>
public sealed partial class BlueprintStore(string directory)
{
    /// <summary>
    /// The folder holding the blueprint files (created on the first save).
    /// </summary>
    public string Directory { get; } = directory;

    /// <summary>
    /// The store in the app's data folder (blueprints/).
    /// </summary>
    public static BlueprintStore Default => new(Path.Combine(AppSettings.DataDir, "blueprints"));

    /// <summary>
    /// What the list shows of one blueprint file.
    /// </summary>
    /// <param name="Id">The file name without .json: the key for <c>Read</c> and <c>Delete</c>.</param>
    /// <param name="Name">The blueprint's name (the id when the file has none).</param>
    /// <param name="Created">When it was saved (the file's write time when not recorded).</param>
    /// <param name="World">The world it was copied from, if known.</param>
    /// <param name="W">The copy's ground grid width (points, from the clipboard's "w").</param>
    /// <param name="H">The copy's ground grid height (points, from the clipboard's "h").</param>
    /// <param name="Objects">How many objects the copy holds.</param>
    /// <param name="Ground">Whether the copy carries ground (see <c>HasGround</c>).</param>
    /// <param name="Thumb">The small picture as a data URL, if any.</param>
    /// <param name="Source">Where the copy came from (another mod's format, for instance), shown in the list; null if
    /// none.</param>
    public sealed record Summary(string Id, string Name, DateTime Created, string? World, int W, int H, int Objects, bool Ground, string? Thumb, string? Source);

    /// <summary>
    /// The whole file, or null when there is no such blueprint.
    /// </summary>
    public string? Read(string id) => ValidId(id) && File.Exists(PathOf(id)) ? File.ReadAllText(PathOf(id)) : null;

    /// <summary>
    /// Removes a blueprint's file; false when the id is not valid or there is no such file.
    /// </summary>
    public bool Delete(string id)
    {
        if (!ValidId(id) || !File.Exists(PathOf(id)))
        {
            return false;
        }
        File.Delete(PathOf(id));
        return true;
    }

    /// <summary>
    /// Whether a blueprint with this name (by its file name) is already saved.
    /// </summary>
    public bool Exists(string name) => File.Exists(PathOf(IdFor(name.Trim())));

    /// <summary>
    /// The file of a blueprint id.
    /// </summary>
    private string PathOf(string id) => Path.Combine(Directory, id + ".json");
}

using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Saved copies ("blueprints", like WorldEdit's schematics): what the Area or Select tool copied, kept as
/// one JSON file each in the app's data folder (blueprints/), so they can be pasted into any world.
/// A file holds the name, where it came from, a small picture and the copy itself in the editor's
/// clipboard format (ground shape, paint and objects relative to the copy's centre).
/// </summary>
public sealed class BlueprintStore(string directory)
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
    /// The blueprints in the folder, by name (case ignored). Files that are not blueprints or cannot be read are left
    /// out.
    /// </summary>
    public List<Summary> List()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }
        List<Summary> list = [];
        foreach (string file in System.IO.Directory.GetFiles(Directory, "*.json"))
        {
            try
            {
                var doc = JsonNode.Parse(File.ReadAllText(file));
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
        return [.. list.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The whole file, or null when there is no such blueprint.
    /// </summary>
    public string? Read(string id) => ValidId(id) && File.Exists(PathOf(id)) ? File.ReadAllText(PathOf(id)) : null;

    /// <summary>
    /// Saves a blueprint under a file name made from its name; a blueprint with the same name is replaced. Throws
    /// <c>ArgumentException</c> for an empty name or a clip that is not the editor's copy format; the file is written
    /// through a .tmp file so a half-written one never replaces it.
    /// </summary>
    /// <param name="name">The blueprint's name (trimmed).</param>
    /// <param name="world">The world it was copied from, or null.</param>
    /// <param name="thumb">The small picture as a data URL, or null.</param>
    /// <param name="clip">The copy in the editor's clipboard format (deep-copied into the file).</param>
    /// <param name="source">Where the copy came from, or null.</param>
    /// <returns>The blueprint's id (its file name without .json).</returns>
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
    /// A file name from the blueprint's name: letters, digits, spaces, dashes, underscores and dots are kept. Other
    /// characters become "_", leading and trailing dots and spaces are dropped; at most 80 characters, "blueprint"
    /// when nothing is left.
    /// </summary>
    public static string IdFor(string name)
    {
        string id = new([.. name.Trim().Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' ? c : '_')]);
        id = id.Trim('.', ' ');
        if (id.Length == 0)
        {
            id = "blueprint";
        }
        return id.Length > 80 ? id[..80] : id;
    }

    /// <summary>
    /// An id that <c>IdFor</c> leaves as it is and holds no folder part, so it cannot point outside the folder.
    /// </summary>
    private static bool ValidId(string id) => id.Length > 0 && id == IdFor(id) && Path.GetFileName(id) == id;

    /// <summary>
    /// The file of a blueprint id.
    /// </summary>
    private string PathOf(string id) => Path.Combine(Directory, id + ".json");

    /// <summary>
    /// The copy has ground when any point of its relative heights is set (-32768 marks "no ground").
    /// </summary>
    private static bool HasGround(JsonObject clip) => clip["rel"] is JsonArray rel && rel.Any(v => v != null && (int)v != -32768);
}

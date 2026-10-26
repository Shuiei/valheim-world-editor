using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Saving a blueprint for <see cref="BlueprintStore"/>.
/// </summary>
public sealed partial class BlueprintStore
{
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
        SafeFile.WriteAllText(PathOf(id), doc.ToJsonString());
        return id;
    }
}

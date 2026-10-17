using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What a container, a sign or a creature takes in set_contents: its items, its text and its stars.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// The items a container takes for set_contents, laid out in its slots in order; or why the object has no such slots or
    /// the items do not fit in them.
    /// </summary>
    /// <param name="prefab">The object's prefab.</param>
    /// <param name="name">Its name, for the answers.</param>
    /// <param name="items">The items given, in order.</param>
    internal static (List<ItemUpload>? Uploads, string? Refusal) ContainerItems(int prefab, string name, ItemSpec[] items)
    {
        var info = Terrain.PrefabCatalog.Details(prefab);
        if (info is not { ContainerW: > 0 } box)
        {
            return (null, $"{name} is not a container or a stand.");
        }
        if (items.Length > box.ContainerW * box.ContainerH)
        {
            return (null, $"{name} has {box.ContainerW * box.ContainerH} slots, not {items.Length}.");
        }
        List<ItemUpload> uploads = [.. items.Select((it, i) => new ItemUpload(it.Item, null, Math.Max(1, it.Stack), Math.Max(1, it.Quality), X: i % box.ContainerW, Y: i / box.ContainerW, Variant: Math.Max(0, it.Variant)))];
        return (uploads, null);
    }

    /// <summary>
    /// A sign's text and a creature's stars for set_contents, into <paramref name="set"/>; or why the object is neither a sign
    /// (for the text) nor a creature (for the stars).
    /// </summary>
    /// <param name="prefab">The object's prefab.</param>
    /// <param name="name">Its name, for the answers.</param>
    /// <param name="text">The sign's text, if given.</param>
    /// <param name="stars">The creature's stars, if given.</param>
    /// <param name="set">The field changes so far; the text and stars are added to it.</param>
    internal static string? TextAndStars(int prefab, string name, string? text, int? stars, List<FieldChange> set)
    {
        if (text != null)
        {
            if (!name.Contains("sign", StringComparison.OrdinalIgnoreCase))
            {
                return $"{name} is not a sign: only signs take a text.";
            }
            set.Add(new("strings", "text", text));
        }
        if (stars is int st)
        {
            if (!Terrain.PrefabCatalog.IsCreature(prefab))
            {
                return $"{name} is not a creature: only creatures have stars.";
            }
            set.Add(new("ints", "level", (Math.Clamp(st, 0, 2) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        return null;
    }
}

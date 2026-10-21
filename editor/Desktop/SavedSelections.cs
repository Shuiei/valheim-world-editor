using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Saved selections, per world (the web editor kept them in the browser): a name and, for each object, its kind and
/// place, since the ids change when the world is saved. Picking one finds the objects again by kind and place (within 5
/// cm across, 20 cm up). In selections.json in the data folder.
/// </summary>
public static partial class SavedSelections
{
    /// <summary>One object of a saved selection: its kind and world position (kept to the millimetre).</summary>
    /// <param name="Prefab">The prefab's hash.</param>
    /// <param name="X">World x (east).</param>
    /// <param name="Y">World height.</param>
    /// <param name="Z">World z (north).</param>
    public sealed record Item(int Prefab, float X, float Y, float Z);

    /// <summary>A saved selection.</summary>
    /// <param name="Name">Its name, unique per world.</param>
    /// <param name="Items">Its objects.</param>
    public sealed record Saved(string Name, List<Item> Items);

    /// <summary>Tests: another file, never the user's.</summary>
    internal static string? PathOverride { get; set; }

    /// <summary>selections.json in the data folder, or the tests' file.</summary>
    private static string FilePath => PathOverride ?? Path.Combine(AppSettings.DataDir, "selections.json");

    /// <summary>This world's saved selections, oldest first; none when the file could not be read.</summary>
    public static List<Saved> For(string world) => LoadAll() is { } all && all.TryGetValue(world, out var list) ? list : [];

    /// <summary>
    /// Keeps a selection under this name (one of the same name is replaced; the new one goes last). False when the file
    /// could not be read or written.
    /// </summary>
    public static bool Keep(string world, string name, IEnumerable<Item> items)
    {
        if (LoadAll() is not { } all)
        {
            return false;
        }
        var list = all.TryGetValue(world, out var l) ? l : [];
        list.RemoveAll(s => s.Name == name);
        list.Add(new Saved(name, [.. items.Select(i => new Item(i.Prefab, MathF.Round(i.X, 3), MathF.Round(i.Y, 3), MathF.Round(i.Z, 3)))]));
        all[world] = list;
        return SaveAll(all);
    }

    /// <summary>
    /// Forgets the selection of this name (nothing to forget is fine); false when the file could not be read or
    /// written.
    /// </summary>
    public static bool Forget(string world, string name)
    {
        if (LoadAll() is not { } all)
        {
            return false;
        }
        if (!all.TryGetValue(world, out var list) || list.RemoveAll(s => s.Name == name) == 0)
        {
            return true;
        }
        if (list.Count == 0)
        {
            all.Remove(world);
        }
        return SaveAll(all);
    }
}

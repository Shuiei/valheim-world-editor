using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Saved selections, per world (the web editor kept them in the browser): a name and, for each object, its kind and
/// place, since the ids change when the world is saved. Picking one finds the objects again by kind and place (within 5
/// cm across, 20 cm up). In selections.json in the data folder.
/// </summary>
public static class SavedSelections
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

    /// <summary>Every world's saved selections, by world name; empty when the file is missing or unreadable.</summary>
    private static Dictionary<string, List<Saved>> LoadAll()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var all = JsonSerializer.Deserialize<Dictionary<string, List<Saved>>>(File.ReadAllText(FilePath));
                if (all != null)
                {
                    // A hand-edited file may hold nulls: those entries are left out.
                    return all.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value.Where(s => s?.Name != null && s.Items != null).ToList());
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
        }
        return [];
    }

    /// <summary>Writes every world's saved selections; false when the file could not be written.</summary>
    private static bool SaveAll(Dictionary<string, List<Saved>> all)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(all));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>This world's saved selections, oldest first.</summary>
    public static List<Saved> For(string world) => LoadAll().TryGetValue(world, out var list) ? list : [];

    /// <summary>
    /// Keeps a selection under this name (one of the same name is replaced; the new one goes last). False when the file
    /// could not be written.
    /// </summary>
    public static bool Keep(string world, string name, IEnumerable<Item> items)
    {
        var all = LoadAll();
        var list = all.TryGetValue(world, out var l) ? l : [];
        list.RemoveAll(s => s.Name == name);
        list.Add(new Saved(name, [.. items.Select(i => new Item(i.Prefab, MathF.Round(i.X, 3), MathF.Round(i.Y, 3), MathF.Round(i.Z, 3)))]));
        all[world] = list;
        return SaveAll(all);
    }

    /// <summary>
    /// Forgets the selection of this name (nothing to forget is fine); false when the file could not be written.
    /// </summary>
    public static bool Forget(string world, string name)
    {
        var all = LoadAll();
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

    /// <summary>
    /// The objects of a saved selection among these things (indices; the deleted left out, each matched once), and how
    /// many were not found.
    /// </summary>
    public static (List<int> Ids, int Missing) Find(Saved saved, IReadOnlyList<WorldScene.Thing> things)
    {
        var ids = new List<int>();
        int missing = 0;
        foreach (var it in saved.Items)
        {
            int found = -1;
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (!t.Gone && t.Prefab == it.Prefab && MathF.Abs(t.Position.X - it.X) < 0.05f && MathF.Abs(t.Position.Z - it.Z) < 0.05f && MathF.Abs(t.Position.Y - it.Y) < 0.2f && !ids.Contains(i))
                {
                    found = i;
                    break;
                }
            }
            if (found >= 0)
            {
                ids.Add(found);
            }
            else
            {
                missing++;
            }
        }
        return (ids, missing);
    }
}

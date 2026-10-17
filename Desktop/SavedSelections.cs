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

    /// <summary>
    /// Every world's saved selections, by world name; empty when there is no file. One that is not valid JSON is first
    /// copied aside as selections.json.bad (the next save writes over it) and counts as empty. Null when the file
    /// could not be read, or kept aside: it must not be written over.
    /// </summary>
    private static Dictionary<string, List<Saved>>? LoadAll()
    {
        string text;
        try
        {
            // Read at once: File.Exists says no for a file there that cannot be read, which must not count as none.
            text = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Saved selections: {FilePath} could not be read ({ex.Message}); not saving over it.");
            return null;
        }
        try
        {
            var all = JsonSerializer.Deserialize<Dictionary<string, List<Saved>>>(text);
            // A hand-edited file may hold nulls: those entries are left out.
            return all == null ? [] : all.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value.Where(s => s?.Name != null && s.Items != null).ToList());
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            Console.WriteLine($"Saved selections: {FilePath} is not readable ({ex.Message}); kept as {FilePath}.bad, starting from none.");
            try
            {
                File.Copy(FilePath, FilePath + ".bad", overwrite: true);
                return [];
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Saved selections: {FilePath} could not be kept as {FilePath}.bad ({copy.Message}); not saving over it.");
                return null;
            }
        }
    }

    /// <summary>
    /// Writes every world's saved selections through a temporary file, so a write cut short leaves the old file whole;
    /// false when the file could not be written.
    /// </summary>
    private static bool SaveAll(Dictionary<string, List<Saved>> all)
    {
        try
        {
            SafeFile.WriteAllText(FilePath, JsonSerializer.Serialize(all));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Saved selections: {FilePath} could not be written ({ex.Message}).");
            return false;
        }
    }

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

    /// <summary>
    /// The objects of a saved selection among these things (indices; the deleted left out, each matched once: the
    /// first of the matching ones not taken yet), and how many were not found. The things are put in metre cells by
    /// kind once, so each item looks only at the cells it can match in.
    /// </summary>
    public static (List<int> Ids, int Missing) Find(Saved saved, IReadOnlyList<WorldScene.Thing> things)
    {
        var cells = new Dictionary<(int Prefab, int X, int Z), List<int>>();
        for (int i = 0; i < things.Count; i++)
        {
            var t = things[i];
            if (t.Gone)
            {
                continue;
            }
            var key = (t.Prefab, Cell(t.Position.X), Cell(t.Position.Z));
            if (!cells.TryGetValue(key, out var list))
            {
                cells[key] = list = [];
            }
            list.Add(i);
        }
        var ids = new List<int>();
        var taken = new HashSet<int>();
        int missing = 0;
        foreach (var it in saved.Items)
        {
            // Within 5 cm across: in the item's cell or one next to it.
            int found = -1, cx = Cell(it.X), cz = Cell(it.Z);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!cells.TryGetValue((it.Prefab, cx + dx, cz + dz), out var list))
                    {
                        continue;
                    }
                    foreach (int i in list)
                    {
                        // Each cell's list is in the things' order: past the first match, none comes earlier.
                        if (found >= 0 && i > found)
                        {
                            break;
                        }
                        var t = things[i];
                        if (MathF.Abs(t.Position.X - it.X) < 0.05f && MathF.Abs(t.Position.Z - it.Z) < 0.05f && MathF.Abs(t.Position.Y - it.Y) < 0.2f && !taken.Contains(i))
                        {
                            found = i;
                            break;
                        }
                    }
                }
            }
            if (found >= 0)
            {
                ids.Add(found);
                taken.Add(found);
            }
            else
            {
                missing++;
            }
        }
        return (ids, missing);
    }

    /// <summary>The metre cell a coordinate is in, for <see cref="Find"/>.</summary>
    private static int Cell(float v) => (int)MathF.Floor(v);
}

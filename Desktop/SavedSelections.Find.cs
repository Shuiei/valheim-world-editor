
namespace TerrainEditor.Desktop;

// Finding a saved selection's objects again among the world's things, by kind and place.
public static partial class SavedSelections
{
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

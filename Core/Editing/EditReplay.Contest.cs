using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>
    /// Where the editor's save put an object at the very place of a deleted one of its kind (a piece claimed, an object
    /// turned in place), the game's object there is either: the one whose data it has gets it (the original, when the
    /// game saved from the save it had; the editor's, when it saved from the editor's save). When neither's data is
    /// the game's, the game changed it: the original gets it (a clash, the editor's object going with it).
    /// </summary>
    private void Contest(HashSet<int> deleted, Saved saved)
    {
        var at = new Dictionary<(int, long, long, long), Queue<NewObject>>();
        foreach (var n in _savedAdds.Values.OrderByDescending(n => n.Id))
        {
            var key = ObjectPlaces.Key(n.Prefab, n.Position);
            if (!at.TryGetValue(key, out var q))
            {
                at[key] = q = new Queue<NewObject>();
            }
            q.Enqueue(n);
        }
        foreach (int s in deleted.Union(saved.Deleted).Order())
        {
            var o = _old.ObjectRefs[s];
            if (!at.TryGetValue(ObjectPlaces.Key(o.Prefab, o.Position), out var q) || q.Count == 0)
            {
                continue;
            }
            var n = q.Dequeue();
            int? y = _places.Take(o.Prefab, o.Position);
            if (y is int g && SameData(s, g) is not true && WrittenBy(n) is { } mine && NewData(g) is { } now && mine.AsSpan().SequenceEqual(now))
            {
                _found[n.Id] = g;
                _found[s] = _places.Take(o.Prefab, o.Position);
            }
            else
            {
                _found[s] = y;
                _found[n.Id] = _places.Take(n.Prefab, n.Position);
            }
        }
    }

    /// <summary>
    /// An added object's data as the editor's save wrote it (as the editor writes it now); null when it cannot be made
    /// again (its source's chunk file is gone, or its data is damaged).
    /// </summary>
    private byte[]? WrittenBy(NewObject n)
    {
        try
        {
            return _old.NewObjectBytes(n, _oldSource) is { } bytes ? Written(bytes, 41) : null;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException || CheckedReads.Damaged(ex))
        {
            Log.CaughtBrief(ex, "reading a new object's bytes again for the replay (taken as unread)");
            return null;
        }
    }

    /// <summary>An object's data in the game's save, as the editor writes it; null when it is damaged.</summary>
    private byte[]? NewData(int id)
    {
        var o = Newer.ObjectRefs[id];
        return Written(_newSource(o)[(int)o.Start..(int)o.End], o.File.WorldVersion);
    }

    /// <summary>Notes an object change of an object of the save read that clashes.</summary>
    private void Clashing(Item item)
    {
        var o = _old.ObjectRefs[item.Old];
        Clashing(item, o.Prefab, o.Position, _unread.Contains(item.Old));
    }

    /// <summary>Notes an object change that clashes.</summary>
    /// <param name="item">The change.</param>
    /// <param name="prefab">The object's prefab hash.</param>
    /// <param name="position">Where it stood.</param>
    /// <param name="unsure">Whether the game may not have changed it (the data cannot be compared).</param>
    private void Clashing(Item item, int prefab, Vector3 position, bool unsure)
    {
        _items.Add(item);
        _clashes.Add(new Clash(item.Kind, prefab, position, unsure));
    }
}

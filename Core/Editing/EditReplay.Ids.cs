using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>An added object the editor's save wrote, still as written, found in the game's save (made from that one).</summary>
    private bool InGameSave(NewObject n) => _savedAdds.TryGetValue(n.Id, out var s) && s == n && FindSaved(n.Id) != null;

    /// <summary>An object of the save read, in the game's save (null: not there); each found once.</summary>
    /// <param name="id">Its id in the save read.</param>
    private int? Find(int id)
    {
        if (!_found.TryGetValue(id, out int? y))
        {
            var o = _old.ObjectRefs[id];
            _found[id] = y = _places.Take(o.Prefab, o.Position);
        }
        return y;
    }

    /// <summary>An added object the editor's save wrote, in the game's save (null: not there); each found once.</summary>
    /// <param name="id">Its own (negative) id.</param>
    private int? FindSaved(int id)
    {
        if (!_found.TryGetValue(id, out int? y))
        {
            var n = _savedAdds[id];
            _found[id] = y = _places.Take(n.Prefab, n.Position);
        }
        return y;
    }

    /// <summary>
    /// An object of the save the editor read, as the game's save has it (the history's steps are fitted with it): its id
    /// there, or the one it came back under; an added object the editor's save wrote, its id in the game's save. Null:
    /// none (an object the game removed; an added object keeps its own id).
    /// </summary>
    /// <param name="id">Its id in the save read, or an added object's own (negative) id.</param>
    public int? NewerId(int id)
    {
        if (id >= 0)
        {
            return _back.TryGetValue(id, out int b) ? b : Find(id);
        }
        return _savedAdds.ContainsKey(id) ? FindSaved(id) : null;
    }

    /// <summary>
    /// An object of the save read that the game's save lacks, as an object to add (to come back with an undo or redo,
    /// or after a deletion undone): its data as read, else a new object of its kind.
    /// </summary>
    /// <param name="oldId">Its id in the save read.</param>
    /// <param name="id">The id it gets (negative, from the session).</param>
    public NewObject Gone(int oldId, int id)
    {
        var o = _old.ObjectRefs[oldId];
        byte[]? raw = OldData(oldId);
        var rotation = raw != null ? ZdoData.Parse(raw).Rotation : Vector3.Zero;
        return new NewObject(id, o.Prefab, o.Position, rotation, 0f, Fresh: raw == null, Raw: raw);
    }

    /// <summary>
    /// An added object fitted to the game's save: what it was copied from, found there; not found, a moved object keeps
    /// the data it had (<see cref="Restored"/>), a new one is made from its kind.
    /// </summary>
    private NewObject Remapped(NewObject n)
    {
        if (n.SourceId is not int s || s < 0)
        {
            return n;
        }
        if (Find(s) is int y)
        {
            return n with { SourceId = y };
        }
        return !n.Fresh && n.Raw == null ? Restored(n, s) : n with { SourceId = null };
    }

    /// <summary>
    /// A moved or edited object whose source the game's save lacks, kept whole: its own data, else its source's as read,
    /// else a new object of its kind (the data could not be read again).
    /// </summary>
    private NewObject Restored(NewObject n, int source)
    {
        byte[]? raw = n.Raw ?? OldData(source);
        return raw != null ? n with { SourceId = null, Raw = raw } : n with { SourceId = null, Fresh = true };
    }

    /// <summary>
    /// Whether an object has the same data in both saves; null when its data as read cannot be read again (the game
    /// removed that save) or either is damaged.
    /// </summary>
    private bool? SameData(int oldId, int newerId)
    {
        byte[]? was = OldData(oldId);
        if (was == null)
        {
            return null;
        }
        byte[]? now = NewData(newerId);
        if (now == null)
        {
            _unread.Add(oldId);
            return null;
        }
        return was.AsSpan().SequenceEqual(now);
    }
}

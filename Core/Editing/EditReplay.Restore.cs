using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
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

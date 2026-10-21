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
}

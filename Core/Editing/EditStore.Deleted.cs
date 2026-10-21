namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>
    /// Whether an object is gone: a saved one (id 0 and up) left out of the next save, or one added in this session
    /// (a negative id) no longer added (in the trash, or never added).
    /// </summary>
    /// <param name="id">The object's id.</param>
    public bool IsGone(int id)
    {
        lock (_lock)
        {
            return id >= 0 ? _deleted.Contains(id) : !_added.ContainsKey(id);
        }
    }

    /// <summary>
    /// Marks saved objects to be left out of the save, or not. An added object (negative id) is moved to the
    /// trash instead, or back from it.
    /// </summary>
    public void SetDeleted(IEnumerable<int> ids, bool deleted)
    {
        lock (_lock)
        {
            foreach (int id in ids)
            {
                if (id < 0)
                {
                    // An object added in this session: deleting it just drops it.
                    if (deleted && _added.Remove(id, out NewObject? o))
                    {
                        _addedTrash[id] = o;
                    }
                    else if (!deleted && _addedTrash.Remove(id, out NewObject? t))
                    {
                        _added[id] = t;
                    }
                    continue;
                }
                if (deleted)
                {
                    _deleted.Add(id);
                }
                else
                {
                    _deleted.Remove(id);
                }
            }
            Version++;
            ObjectsVersion++;
        }
    }
}

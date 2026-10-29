
namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>
    /// Answers from the objects to leave out (by WorldSave id) and the objects added (by their negative id) as they
    /// are, under the store's lock: for counts that would otherwise copy both at every status refresh. The answer must
    /// not keep either.
    /// </summary>
    public T WithObjects<T>(Func<IReadOnlySet<int>, IReadOnlyDictionary<int, NewObject>, T> answer)
    {
        lock (_lock)
        {
            return answer(_deleted, _added);
        }
    }

    /// <summary>A copy of the objects added and deleted again, or kept for a history (<see cref="KeepGone"/>): what undo can bring back.</summary>
    public List<NewObject> Gone
    {
        get
        {
            lock (_lock)
            {
                return [.. _addedTrash.Values];
            }
        }
    }

    /// <summary>An object added in this session (also one deleted again, which undo can bring back).</summary>
    public NewObject? FindAdded(int id)
    {
        lock (_lock)
        {
            return _added.TryGetValue(id, out NewObject? o) ? o : _addedTrash.TryGetValue(id, out NewObject? t) ? t : null;
        }
    }

    /// <summary>Adds new objects (those with a negative id; others are ignored), taking them out of the trash.</summary>
    public void AddObjects(IEnumerable<NewObject> objects)
    {
        lock (_lock)
        {
            foreach (NewObject o in objects.Where(o => o.Id < 0))
            {
                _addedTrash.Remove(o.Id);
                _added[o.Id] = o;
            }
            Version++;
            ObjectsVersion++;
        }
    }

    /// <summary>
    /// Objects a kept history can bring back (gone now): with the ones added and deleted again, so an
    /// undo or redo adds them. Nothing is pending until then.
    /// </summary>
    public void KeepGone(IEnumerable<NewObject> objects)
    {
        lock (_lock)
        {
            foreach (NewObject o in objects.Where(o => o.Id < 0))
            {
                _addedTrash[o.Id] = o;
            }
        }
    }

    /// <summary>A copy of the zone resets to do.</summary>
    public List<ZoneReset> Resets
    {
        get
        {
            lock (_lock)
            {
                return [.. _resets.Values];
            }
        }
    }

    /// <summary>How many zones are to be reset.</summary>
    public int ResetCount
    {
        get
        {
            lock (_lock)
            {
                return _resets.Count;
            }
        }
    }

    /// <summary>Asks for a zone reset (replacing any for that zone), or drops it.</summary>
    public void SetReset(ZoneReset reset, bool on)
    {
        lock (_lock)
        {
            if (on)
            {
                _resets[(reset.X, reset.Z)] = reset;
            }
            else
            {
                _resets.Remove((reset.X, reset.Z));
            }
            Version++;
        }
    }

    /// <summary>
    /// Live: the zone resets as they were sent to the game (a copy taken when sending), done there and no longer
    /// pending. A zone marked again with other options while they were on the way stays marked, to be sent next.
    /// </summary>
    public void MarkResetsApplied(IEnumerable<ZoneReset> sent)
    {
        lock (_lock)
        {
            foreach (ZoneReset reset in sent)
            {
                if (_resets.TryGetValue((reset.X, reset.Z), out ZoneReset? now) && now == reset)
                {
                    _resets.Remove((reset.X, reset.Z));
                }
            }
            Version++;
        }
    }
}

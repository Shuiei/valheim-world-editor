using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>
    /// An object's data in the save read, as the editor writes it now (so saves of two formats compare); null, and
    /// noted, when it cannot be read again: the game removes older saves, chunk files included.
    /// </summary>
    private byte[]? OldData(int id)
    {
        if (_oldData.TryGetValue(id, out byte[]? data))
        {
            return data;
        }
        var o = _old.ObjectRefs[id];
        try
        {
            data = Written(_oldSource(o)[(int)o.Start..(int)o.End], o.File.WorldVersion);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            Log.CaughtBrief(ex, "reading an object of the older save for the replay (its chunk file is gone: taken as unread)");
            data = null;
        }
        if (data == null)
        {
            _unread.Add(id);
        }
        return _oldData[id] = data;
    }

    /// <summary>An object's saved bytes as the editor writes them; null when they are damaged.</summary>
    private static byte[]? Written(byte[] bytes, int worldVersion)
    {
        try
        {
            return ZdoData.Parse(bytes, worldVersion).Serialize();
        }
        catch (Exception ex) when (CheckedReads.Damaged(ex))
        {
            Log.CaughtBrief(ex, "reading an object's saved bytes for the replay (damaged)");
            return null;
        }
    }

    /// <summary>
    /// Makes the edits the game's save with the pending changes on it: the store starts from that save, takes every
    /// change that does not clash, and of those that do, the editor's where <paramref name="keepMine"/> says so (a zone
    /// then keeps the editor's points and paint over the game's, and the game's elsewhere), else the game's. Once
    /// only. Null, with nothing changed, when the edits changed since they were taken.
    /// </summary>
    /// <param name="keepMine">
    /// Per kind of clash: true keeps the editor's change, false the game's version (asked only for kinds that clash).
    /// </param>
    /// <param name="edits">The pending edits, taken by <see cref="Prepare"/>.</param>
    /// <param name="nextId">Gives an id to an object that comes back (negative, from the session).</param>
    /// <exception cref="InvalidOperationException">Applied already.</exception>
    public Replayed? Apply(Func<ReplayClash, bool> keepMine, EditStore edits, Func<int> nextId)
    {
        if (_version < 0)
        {
            throw new InvalidOperationException("This replay was applied already.");
        }
        if (edits.Version != _version)
        {
            return null;
        }
        // Read before the store changes: what comes back is the save read's.
        var back = _comeBack.Select(s => (Old: s, Object: Gone(s, nextId()))).ToList();
        edits.ResetFrom(Newer);
        _version = -1;
        List<int> deletes = [.. _deletes];
        List<NewObject> adds = [.. _adds];
        List<NewObject> gone = [.. _gone];
        var dropped = new HashSet<int>();
        var gameGround = new HashSet<(int X, int Z)>();
        foreach (var zone in _zones)
        {
            edits.Put(zone.Clone());
        }
        bool ground = _zoneClashes.Count > 0 && keepMine(ReplayClash.Ground);
        foreach (var (mine, read, last, game) in _zoneClashes)
        {
            if (ground)
            {
                edits.Put(Merged(mine, read, last, game));
            }
            else
            {
                gameGround.Add((mine.ZoneX, mine.ZoneZ));
            }
        }
        var kept = new Dictionary<ReplayClash, bool>();
        foreach (var item in _items)
        {
            if (!kept.TryGetValue(item.Kind, out bool mine))
            {
                kept[item.Kind] = mine = keepMine(item.Kind);
            }
            if (mine)
            {
                if (item.Newer is int y)
                {
                    deletes.Add(y);
                }
                adds.AddRange(item.Mine);
            }
            else
            {
                gone.AddRange(item.Original);
                dropped.Add(item.Old);
                dropped.UnionWith(item.Original.Select(n => n.Id));
            }
        }
        foreach (var (s, o) in back)
        {
            adds.Add(o);
            _back[s] = o.Id;
        }
        edits.SetDeleted(deletes, true);
        edits.KeepGone(gone);
        edits.AddObjects(adds);
        // A zone left to the game's ground is not reset to generated ground either (that would take the game's ground
        // away); a reset of its objects alone stays.
        foreach (var reset in _resets.Where(r => !r.Ground || !gameGround.Contains((r.X, r.Z))))
        {
            edits.SetReset(reset, true);
        }
        return new Replayed(edits.ChangedZoneCount, edits.DeletedCount, edits.AddedCount, edits.ResetCount, dropped, gameGround, _unread.Count);
    }
}

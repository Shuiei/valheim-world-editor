using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>What the game changed too, of what the editor changed (an <see cref="EditReplay"/> clash).</summary>
public enum ReplayClash
{
    /// <summary>A zone whose ground both changed.</summary>
    Ground,
    /// <summary>An object whose data the game changed, which the editor deleted, moved or edited.</summary>
    Changed,
    /// <summary>An object the game removed (or moved away), which the editor moved or edited.</summary>
    Removed,
}

/// <summary>
/// The pending edits of an open world brought onto a newer save of it, one the game made after the editor read the
/// world (Valheim played between two saves). Every pending change is made again on that save: the changed zones'
/// ground (heights, paint, smoothing, No limit ground), deleted objects, added and moved objects with their data, and
/// zone resets. Ids change at every save, so objects are found again by kind and place (<see cref="ObjectPlaces"/>).
/// What the game changed too is listed (<see cref="Clashes"/>), and <see cref="Apply"/> is told, per kind, whose
/// version stays. Nothing is written to disk: the edits wait in the store, to be saved as usual.
/// </summary>
/// <remarks>
/// The edits are taken against the save the world was read from, as WorldSession.Save writes them. When the editor
/// saved during the session, the game's save may be made from the editor's save (then what it wrote is found there
/// already, and is not made twice) or from the save the game had in memory (then it is made again).
/// </remarks>
public sealed class EditReplay
{
    /// <summary>What the editor's own saves of the session wrote, as WorldSession keeps it (empty before any save).</summary>
    /// <param name="Zones">The zones it wrote ground for.</param>
    /// <param name="Deleted">The deletions as of its last save.</param>
    /// <param name="Added">The added objects as of its last save.</param>
    public sealed record Saved(IReadOnlySet<(int X, int Z)> Zones, IReadOnlySet<int> Deleted, IReadOnlySet<NewObject> Added)
    {
        /// <summary>No save made in the session.</summary>
        public static Saved None { get; } = new(new HashSet<(int X, int Z)>(), new HashSet<int>(), new HashSet<NewObject>());
    }

    /// <summary>Something the game changed too, of what the editor changed.</summary>
    /// <param name="Kind">What kind of clash it is: the user chooses per kind.</param>
    /// <param name="Prefab">The object's prefab hash (0 for a zone).</param>
    /// <param name="Position">Where the object stood when the editor read the world (a zone: its middle).</param>
    /// <param name="Unsure">
    /// The object's data as the editor read it could not be read again (the game removed that save): the game may not
    /// have changed it.
    /// </param>
    public sealed record Clash(ReplayClash Kind, int Prefab, Vector3 Position, bool Unsure)
    {
        /// <summary>The 64 m zone it is in.</summary>
        public (int X, int Z) Zone => ((int)MathF.Floor((Position.X + 32f) / 64f), (int)MathF.Floor((Position.Z + 32f) / 64f));
    }

    /// <summary>What <see cref="Apply"/> did.</summary>
    /// <param name="Zones">Zones whose ground is pending now.</param>
    /// <param name="Deleted">Objects of the game's save to delete.</param>
    /// <param name="Added">Objects to add (moved ones included).</param>
    /// <param name="Resets">Zones to reset.</param>
    /// <param name="Dropped">
    /// The editor's objects whose change was dropped for the game's version: ids of the save the editor read, and the
    /// added objects' own (negative) ids. A step of the history that touches one cannot be undone any more.
    /// </param>
    /// <param name="GameGround">The zones whose ground stays as the game has it (the editor's changes there dropped).</param>
    /// <param name="Unread">
    /// Objects whose data, as the editor read them, could not be read again (the game removed that save): compared as
    /// maybe changed, or brought back as new objects of their kind without their data.
    /// </param>
    public sealed record Replayed(int Zones, int Deleted, int Added, int Resets, IReadOnlySet<int> Dropped, IReadOnlySet<(int X, int Z)> GameGround, int Unread);

    /// <summary>An object change of the editor's that clashes with the game's: kept or dropped as the user chooses.</summary>
    /// <param name="Kind">Its kind.</param>
    /// <param name="Old">The object as the editor read it (its id there).</param>
    /// <param name="Newer">The object in the game's save (null: the game has it no more).</param>
    /// <param name="Mine">The objects added when the editor's change is kept (a move's copy at its new place).</param>
    /// <param name="Original">The editor's objects as they were (kept as gone objects when the game's version stays).</param>
    private sealed record Item(ReplayClash Kind, int Old, int? Newer, List<NewObject> Mine, List<NewObject> Original);

    /// <summary>The save the world was read from, which the edits are made against.</summary>
    private readonly WorldSave _old;

    /// <summary>The edits' version when they were taken: changed since, <see cref="Apply"/> refuses (-1 once applied).</summary>
    private int _version;

    /// <summary>The game's save's objects by kind and place, each taken once found.</summary>
    private readonly ObjectPlaces _places;

    /// <summary>
    /// Objects found in the game's save (null: not there), by their id in the save the editor read, or by their own
    /// (negative) id for objects the editor's save wrote.
    /// </summary>
    private readonly Dictionary<int, int?> _found = [];

    /// <summary>Objects deleted then undone after the editor's save, brought back: their id in the save read, their new one.</summary>
    private readonly Dictionary<int, int> _back = [];

    /// <summary>The added objects as of the editor's last save, by id.</summary>
    private readonly Dictionary<int, NewObject> _savedAdds = [];

    /// <summary>
    /// Objects of the save read that the editor deleted where its save wrote another of their kind (a piece claimed,
    /// an object turned in place), whose place in the game's save went to the original: that other object, by the
    /// original's id.
    /// </summary>
    private readonly Dictionary<int, NewObject> _contested = [];

    /// <summary>Reads the chunk files of the save read, each once.</summary>
    private readonly Func<ObjectRef, byte[]> _oldSource;

    /// <summary>Reads the chunk files of the game's save, each once.</summary>
    private readonly Func<ObjectRef, byte[]> _newSource;

    /// <summary>The data of objects of the save read, as written now (null: could not be read), by id.</summary>
    private readonly Dictionary<int, byte[]?> _oldData = [];

    /// <summary>Objects of the save read whose data could not be read again.</summary>
    private readonly HashSet<int> _unread = [];

    /// <summary>The zones taken as they are (no clash).</summary>
    private readonly List<ZoneEdit> _zones = [];

    /// <summary>The zones both changed: the editor's, as read, as the game has it.</summary>
    private readonly List<(ZoneEdit Mine, ZoneEdit Read, ZoneEdit Game)> _zoneClashes = [];

    /// <summary>Objects of the game's save to delete whatever is chosen.</summary>
    private readonly List<int> _deletes = [];

    /// <summary>Objects to add whatever is chosen.</summary>
    private readonly List<NewObject> _adds = [];

    /// <summary>The objects added and deleted again (undo can bring them back), fitted to the game's save.</summary>
    private readonly List<NewObject> _gone = [];

    /// <summary>Deletions undone after the editor's save that the game's save lacks: brought back (ids in the save read).</summary>
    private readonly List<int> _comeBack = [];

    /// <summary>The object changes that clash.</summary>
    private readonly List<Item> _items = [];

    /// <summary>The zone resets.</summary>
    private readonly List<ZoneReset> _resets = [];

    /// <summary>Every clash: the zones', then the objects'.</summary>
    private readonly List<Clash> _clashes = [];

    /// <summary>The game's save the edits are brought onto.</summary>
    public WorldSave Newer { get; }

    /// <summary>What the game changed too, of what the editor changed: zones first, then objects.</summary>
    public IReadOnlyList<Clash> Clashes => _clashes;

    /// <summary>Starts a replay (see <see cref="Prepare"/>).</summary>
    private EditReplay(WorldSave old, WorldSave newer, int version)
    {
        _old = old;
        Newer = newer;
        _version = version;
        _places = ObjectPlaces.Of(newer);
        _oldSource = old.SourceReader();
        _newSource = newer.SourceReader();
    }

    /// <summary>
    /// Works out how the pending edits go onto the game's save, and what clashes; nothing changes yet (the store and the
    /// disk are as they were).
    /// </summary>
    /// <param name="old">The save the world was read from (the edits' ids are its).</param>
    /// <param name="edits">The pending edits.</param>
    /// <param name="saved">What the editor's saves of the session wrote.</param>
    /// <param name="newer">The game's save.</param>
    public static EditReplay Prepare(WorldSave old, EditStore edits, Saved saved, WorldSave newer)
    {
        int version = edits.Version;
        var r = new EditReplay(old, newer, version);
        foreach (var n in saved.Added)
        {
            r._savedAdds.TryAdd(n.Id, n);
        }
        r.PlanZones(edits, saved);
        r.PlanObjects(edits.Deleted, edits.Added, edits.Gone, saved);
        r._resets.AddRange(edits.Resets);
        return r;
    }

    /// <summary>
    /// The zones the edits change since the save read (those changed and those the editor's save wrote): a zone the
    /// game left as the editor read it, or as the editor saved it, takes the edits as they are; another clashes.
    /// </summary>
    private void PlanZones(EditStore edits, Saved saved)
    {
        var read = Zones(_old);
        var game = Zones(Newer);
        foreach (var mine in edits.Copies(e => e.Changed || saved.Zones.Contains((e.ZoneX, e.ZoneZ))))
        {
            var key = (mine.ZoneX, mine.ZoneZ);
            var was = Ground(read, key);
            var now = Ground(game, key);
            var last = saved.Zones.Contains(key) ? edits.BaselineOf(key.ZoneX, key.ZoneZ) ?? new ZoneEdit(key.ZoneX, key.ZoneZ) : was;
            if (now.SameGround(was) || now.SameGround(last))
            {
                _zones.Add(mine);
            }
            else
            {
                _zoneClashes.Add((mine, was, now));
                _clashes.Add(new Clash(ReplayClash.Ground, 0, new Vector3(key.ZoneX * 64f, 0f, key.ZoneZ * 64f), false));
            }
        }
    }

    /// <summary>A save's zones with terrain data, by zone; of two in one zone the first, as the edit store takes them.</summary>
    private static Dictionary<(int, int), TerrainZone> Zones(WorldSave world)
    {
        var zones = new Dictionary<(int, int), TerrainZone>();
        foreach (var z in world.TerrainZones)
        {
            if (EditStore.Of(z) != null)
            {
                zones.TryAdd((z.ZoneX, z.ZoneZ), z);
            }
        }
        return zones;
    }

    /// <summary>A zone's ground in a save as an edit (no changes when it has no terrain data).</summary>
    private static ZoneEdit Ground(Dictionary<(int, int), TerrainZone> zones, (int X, int Z) key) =>
        zones.TryGetValue(key, out var z) && EditStore.Of(z) is { } e ? e : new ZoneEdit(key.X, key.Z);

    /// <summary>
    /// The object changes since the save read: deletions, moves (a deletion and a copy keeping the object's data),
    /// other additions, and what the editor's own save wrote and was changed since.
    /// </summary>
    private void PlanObjects(HashSet<int> deleted, List<NewObject> added, List<NewObject> gone, Saved saved)
    {
        var moves = added.Where(n => !n.Fresh && n.SourceId is int s && s >= 0 && deleted.Contains(s))
            .GroupBy(n => n.SourceId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var current = added.ToHashSet();
        Contest(deleted, saved);
        // Added, saved, and put where a deleted object of its kind was, then left to the original: added with the
        // original's clash (kept with the editor's change, dropped with the game's), not on its own.
        var attached = new HashSet<NewObject>();
        // Deleted: gone from the game's save too (the game removed it, or the editor's save did), nothing to do.
        foreach (int s in deleted.Where(s => !moves.ContainsKey(s)).Order())
        {
            if (Find(s) is int y)
            {
                if (SameData(s, y) is true)
                {
                    _deletes.Add(y);
                }
                else
                {
                    List<NewObject> with = _contested.TryGetValue(s, out var n) && current.Contains(n) && !InGameSave(n) ? [n] : [];
                    attached.UnionWith(with);
                    Clashing(new Item(ReplayClash.Changed, s, y, [.. with.Select(Remapped)], with));
                }
            }
        }
        // Deleted, saved, then undone: back, unless the game's save has it (made from the save the game had).
        foreach (int s in saved.Deleted.Where(s => !deleted.Contains(s)).Order())
        {
            if (Find(s) == null)
            {
                _comeBack.Add(s);
            }
        }
        // Added, saved, then deleted or changed: gone from the game's save too.
        foreach (var n in saved.Added.Where(n => !current.Contains(n)))
        {
            if (FindSaved(n.Id) is int y)
            {
                _deletes.Add(y);
            }
        }
        var moved = moves.Values.SelectMany(m => m).ToHashSet();
        foreach (var n in added.Where(n => !moved.Contains(n) && !attached.Contains(n) && !InGameSave(n)))
        {
            _adds.Add(Remapped(n));
        }
        foreach (var (s, copies) in moves.OrderBy(m => m.Key))
        {
            var pending = copies.Where(n => !InGameSave(n)).ToList();
            if (pending.Count == 0)
            {
                continue;
            }
            if (Find(s) is int y)
            {
                List<NewObject> mine = [.. pending.Select(n => n with { SourceId = y })];
                if (SameData(s, y) is true)
                {
                    _adds.AddRange(mine);
                    _deletes.Add(y);
                }
                else
                {
                    Clashing(new Item(ReplayClash.Changed, s, y, mine, pending));
                }
            }
            else
            {
                Clashing(new Item(ReplayClash.Removed, s, null, [.. pending.Select(n => Restored(n, s))], pending));
            }
        }
        _gone.AddRange(gone.Select(Remapped));
        // Added and saved, then deleted or changed, and not in the game's save: kept for the history (undo or redo can
        // bring it back).
        var goneIds = gone.Select(n => n.Id).ToHashSet();
        _gone.AddRange(saved.Added.Where(n => !current.Contains(n) && FindSaved(n.Id) == null && !goneIds.Contains(n.Id)).Select(Remapped));
    }

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
                _contested[s] = n;
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
            return null;
        }
    }

    /// <summary>An object's data in the game's save, as the editor writes it; null when it is damaged.</summary>
    private byte[]? NewData(int id)
    {
        var o = Newer.ObjectRefs[id];
        return Written(_newSource(o)[(int)o.Start..(int)o.End], o.File.WorldVersion);
    }

    /// <summary>Notes an object change that clashes.</summary>
    private void Clashing(Item item)
    {
        _items.Add(item);
        var o = _old.ObjectRefs[item.Old];
        _clashes.Add(new Clash(item.Kind, o.Prefab, o.Position, _unread.Contains(item.Old)));
    }

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
        foreach (var (mine, read, game) in _zoneClashes)
        {
            if (ground)
            {
                edits.Put(Merged(mine, read, game));
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
        foreach (var reset in _resets)
        {
            edits.SetReset(reset, true);
        }
        return new Replayed(edits.ChangedZoneCount, edits.DeletedCount, edits.AddedCount, edits.ResetCount, dropped, gameGround, _unread.Count);
    }

    /// <summary>
    /// A zone both changed, the editor's kept: the editor's height and paint where it changed them since the save read,
    /// the game's elsewhere; No limit ground is the editor's alone.
    /// </summary>
    /// <param name="mine">The editor's zone.</param>
    /// <param name="read">The zone as the editor read it.</param>
    /// <param name="game">The zone as the game saved it.</param>
    private static ZoneEdit Merged(ZoneEdit mine, ZoneEdit read, ZoneEdit game)
    {
        const float Same = 1e-4f;
        var m = game.Clone();
        for (int i = 0; i < EditStore.Cells; i++)
        {
            if (mine.Modified[i] != read.Modified[i]
                || mine.Modified[i] && (MathF.Abs(mine.Level[i] - read.Level[i]) > Same || MathF.Abs(mine.Smooth[i] - read.Smooth[i]) > Same))
            {
                m.Modified[i] = mine.Modified[i];
                m.Level[i] = mine.Level[i];
                m.Smooth[i] = mine.Smooth[i];
            }
            bool paint = mine.PaintModified[i] != read.PaintModified[i];
            for (int c = 0; c < 4 && !paint && mine.PaintModified[i]; c++)
            {
                paint = MathF.Abs(mine.Paint[i * 4 + c] - read.Paint[i * 4 + c]) > Same;
            }
            if (paint)
            {
                m.PaintModified[i] = mine.PaintModified[i];
                Array.Copy(mine.Paint, i * 4, m.Paint, i * 4, 4);
            }
            m.Lift[i] = mine.Lift[i];
        }
        return m;
    }
}

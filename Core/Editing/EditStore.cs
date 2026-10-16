using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>
/// The editor's working copy of every zone's terrain modifications. It starts from the world file
/// and receives the browser's edits; nothing here is written to disk (saving is a separate step).
/// Its members take the store's lock (History aside), and zones are handed out as copies.
/// </summary>
public sealed class EditStore
{
    /// <summary>Vertices per zone side (64 m, one per metre, both edges included).</summary>
    public const int Grid = 65;

    /// <summary>Vertices per zone.</summary>
    public const int Cells = Grid * Grid;

    /// <summary>The game clamps level edits to ±this (metres; TerrainComp).</summary>
    public const float MaxLevel = 8f;

    /// <summary>The game clamps smoothing edits to ±this (metres; TerrainComp).</summary>
    public const float MaxSmooth = 1f;

    /// <summary>
    /// No limit: how far the editor moves ground beyond that (the world's ground stays well within
    /// what the game draws: about -400 to +1000 m).
    /// </summary>
    public const float MaxLift = 400f;

    private readonly Lock _lock = new();

    /// <summary>The working copy of each zone with terrain data, by zone.</summary>
    private readonly Dictionary<(int, int), ZoneEdit> _zones = [];

    /// <summary>Bumped on every change so cached map detail can be rebuilt.</summary>
    public int Version { get; private set; }

    /// <summary>Starts from the world's terrain, with nothing pending.</summary>
    public EditStore(WorldSave world)
    {
        Load(world);
    }

    /// <summary>
    /// The page's undo history (its own JSON), kept here so it survives reloads of the page and moves of
    /// the work area. It describes the pending changes, so it goes with them (ResetFrom).
    /// </summary>
    public string? History { get; set; }

    /// <summary>
    /// Changes each time the pending changes are reset: history sent for an earlier generation (a page
    /// closing after a save, for example) is not kept.
    /// </summary>
    public int HistoryGeneration { get; private set; }

    /// <summary>
    /// Start over from a (re)loaded world: after saving, or to discard unsaved changes. Drops every pending
    /// change, deletion, addition and reset, and the history.
    /// </summary>
    public void ResetFrom(WorldSave world)
    {
        lock (_lock)
        {
            History = null;
            HistoryGeneration++;
            _zones.Clear();
            _baseline.Clear();
            _deleted.Clear();
            _added.Clear();
            _addedTrash.Clear();
            _resets.Clear();
            _gameChanged.Clear();
            Load(world);
            Version++;
        }
    }

    /// <summary>
    /// Takes the world's terrain zones as the working copy and the baseline; damaged zones are skipped, and of two
    /// terrain objects in one zone only the first is taken.
    /// </summary>
    private void Load(WorldSave world)
    {
        foreach (TerrainZone z in world.TerrainZones)
        {
            // Two terrain objects in one zone: the first is shown (the writer saves the zone into both).
            if (z.ModifiedHeight.Length != Cells || z.ModifiedPaint.Length != Cells || _zones.ContainsKey((z.ZoneX, z.ZoneZ)))
            {
                continue;
            }
            ZoneEdit edit = FromTerrain(z);
            _zones[(z.ZoneX, z.ZoneZ)] = edit;
            _baseline[(z.ZoneX, z.ZoneZ)] = edit.Clone();
        }
    }

    /// <summary>A zone's terrain data as an edit (marked as in the world).</summary>
    private static ZoneEdit FromTerrain(TerrainZone z)
    {
        ZoneEdit edit = new(z.ZoneX, z.ZoneZ) { ExistsInWorld = true };
        for (int i = 0; i < Cells; i++)
        {
            edit.Modified[i] = z.ModifiedHeight[i];
            edit.Level[i] = z.LevelDelta[i];
            edit.Smooth[i] = z.SmoothDelta[i];
            edit.PaintModified[i] = z.ModifiedPaint[i];
            edit.Paint[i * 4] = z.Paint[i].X;
            edit.Paint[i * 4 + 1] = z.Paint[i].Y;
            edit.Paint[i * 4 + 2] = z.Paint[i].Z;
            edit.Paint[i * 4 + 3] = z.Paint[i].W;
        }
        return edit;
    }

    /// <summary>
    /// Live: zones whose ground the game changed (a player digging, a hoe) while the editor had changes of
    /// its own there, not applied yet, with the game's ground. Applying them would write over what was done
    /// in the game, so the editor asks first; once the editor's changes there are undone the game's ground
    /// comes in (TakeWaitingGround); applying clears it (or discarding).
    /// </summary>
    private readonly Dictionary<(int, int), ZoneEdit> _gameChanged = [];

    /// <summary>The zones whose game ground is waiting (see <c>_gameChanged</c>), as a copy.</summary>
    public IReadOnlyList<(int X, int Z)> GameChanged
    {
        get
        {
            lock (_lock)
            {
                return _gameChanged.Keys.ToList();
            }
        }
    }

    /// <summary>
    /// The zones the game changed under changes of the editor's not applied yet: what an apply would write
    /// over (asked first).
    /// </summary>
    public List<(int X, int Z)> PendingOverGame()
    {
        lock (_lock)
        {
            return [.. _gameChanged.Keys.Where(k => _zones.TryGetValue(k, out ZoneEdit? e) && e.Changed)];
        }
    }

    /// <summary>What became of a zone's ground sent by the game (see <see cref="TakeGameGround"/>).</summary>
    public enum GameGround
    {
        /// <summary>What the editor has as applied: its own apply coming back.</summary>
        Same,
        /// <summary>A zone without changes of the editor's took the game's ground.</summary>
        Taken,
        /// <summary>A zone with changes kept them; the game's ground waits (GameChanged).</summary>
        Kept,
    }

    /// <summary>
    /// Live: a zone's ground as the game has it now (null: no ground changes there any more). Same: what
    /// the editor has as applied (its own apply coming back); Taken: a zone without changes of the
    /// editor's takes it; Kept: one with changes keeps them, the game's ground waiting (GameChanged).
    /// </summary>
    public GameGround TakeGameGround(int zx, int zz, TerrainZone? z)
    {
        var key = (zx, zz);
        ZoneEdit fresh = z != null && z.ModifiedHeight.Length == Cells && z.ModifiedPaint.Length == Cells ? FromTerrain(z) : new ZoneEdit(zx, zz);
        lock (_lock)
        {
            bool same = _baseline.TryGetValue(key, out ZoneEdit? was) ? was.SameGround(fresh) : fresh.IsEmpty;
            if (same)
            {
                _gameChanged.Remove(key);
                return GameGround.Same;
            }
            Version++;
            if (_zones.TryGetValue(key, out ZoneEdit? e) && e.Changed)
            {
                _gameChanged[key] = fresh;
                return GameGround.Kept;
            }
            Take(key, fresh, z != null);
            return GameGround.Taken;
        }
    }

    /// <summary>
    /// Makes the game's ground a zone's baseline and working copy (the working copy dropped when the game has no
    /// terrain data there).
    /// </summary>
    private void Take((int, int) key, ZoneEdit fresh, bool exists)
    {
        _baseline[key] = fresh.Clone();
        _gameChanged.Remove(key);
        if (exists)
        {
            _zones[key] = fresh;
        }
        else
        {
            _zones.Remove(key);
        }
    }

    /// <summary>
    /// Live: zones whose changes of the editor's were undone since the game changed their ground: they
    /// take the game's ground now. Returns them (their ground is to be shown again).
    /// </summary>
    public List<(int X, int Z)> TakeWaitingGround()
    {
        lock (_lock)
        {
            var ready = _gameChanged.Where(kv => !(_zones.TryGetValue(kv.Key, out ZoneEdit? e) && e.Changed)).ToList();
            foreach (var (key, fresh) in ready)
            {
                Take(key, fresh, fresh.ExistsInWorld);
            }
            if (ready.Count > 0)
            {
                Version++;
            }
            return [.. ready.Select(kv => kv.Key)];
        }
    }

    /// <summary>
    /// Each zone as saved / applied: a zone is pending only while it differs from this, so undoing back
    /// to it clears the change.
    /// </summary>
    private readonly Dictionary<(int, int), ZoneEdit> _baseline = [];

    /// <summary>A copy of a zone's working edit, or null when the zone has no terrain data.</summary>
    public ZoneEdit? Get(int zx, int zz)
    {
        lock (_lock)
        {
            return _zones.TryGetValue((zx, zz), out ZoneEdit? e) ? e.Clone() : null;
        }
    }

    /// <summary>Copies of every zone's working edit.</summary>
    public List<ZoneEdit> All()
    {
        lock (_lock)
        {
            return [.. _zones.Values.Select(e => e.Clone())];
        }
    }

    /// <summary>
    /// Copies of the zones' working edits that pass a test, tried on the edits themselves: only those are copied, each
    /// copy being a zone's whole ground (about 127 KB).
    /// </summary>
    /// <param name="keep">The test; it only reads the edit, under the store's lock.</param>
    public List<ZoneEdit> Copies(Func<ZoneEdit, bool> keep)
    {
        lock (_lock)
        {
            return [.. _zones.Values.Where(keep).Select(e => e.Clone())];
        }
    }

    /// <summary>Objects (by WorldSave id) to leave out of the next save.</summary>
    private readonly HashSet<int> _deleted = [];

    /// <summary>A copy of the saved objects to leave out (by WorldSave id).</summary>
    public HashSet<int> Deleted
    {
        get
        {
            lock (_lock)
            {
                return [.. _deleted];
            }
        }
    }

    /// <summary>How many saved objects are to be left out.</summary>
    public int DeletedCount
    {
        get
        {
            lock (_lock)
            {
                return _deleted.Count;
            }
        }
    }

    /// <summary>New objects placed in the editor (planted, pasted, replacements), by the negative id the browser gave them.</summary>
    private readonly Dictionary<int, NewObject> _added = [];

    /// <summary>Added objects that were deleted again (kept so undo can bring them back).</summary>
    private readonly Dictionary<int, NewObject> _addedTrash = [];

    /// <summary>Zones to give back to the world generator.</summary>
    private readonly Dictionary<(int, int), ZoneReset> _resets = [];

    /// <summary>A copy of the objects added and not deleted again.</summary>
    public List<NewObject> Added
    {
        get
        {
            lock (_lock)
            {
                return [.. _added.Values];
            }
        }
    }

    /// <summary>How many objects are added (not counting those deleted again).</summary>
    public int AddedCount
    {
        get
        {
            lock (_lock)
            {
                return _added.Count;
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
        }
    }

    /// <summary>How many zones have pending changes (ground, paint or lift).</summary>
    public int ChangedZoneCount
    {
        get
        {
            lock (_lock)
            {
                return _zones.Values.Count(e => e.Changed);
            }
        }
    }

    /// <summary>Zones that were just applied to the running game: no longer pending.</summary>
    public void MarkApplied(IEnumerable<(int X, int Z)> zones)
    {
        lock (_lock)
        {
            foreach (var key in zones)
            {
                if (_zones.TryGetValue(key, out ZoneEdit? e))
                {
                    e.Changed = false;
                    e.ExistsInWorld = true;
                    _baseline[key] = e.Clone();
                }
                _gameChanged.Remove(key);
            }
            Version++;
        }
    }

    /// <summary>
    /// Live: the zones as they were sent to the game (copies taken when sending). The game has those;
    /// a zone changed again while they were on the way stays changed, to be sent next.
    /// </summary>
    public void MarkApplied(IReadOnlyList<ZoneEdit> sent)
    {
        lock (_lock)
        {
            foreach (ZoneEdit s in sent)
            {
                var key = (s.ZoneX, s.ZoneZ);
                var applied = s.Clone();
                applied.Changed = false;
                applied.ExistsInWorld = true;
                _baseline[key] = applied;
                _gameChanged.Remove(key);
                if (_zones.TryGetValue(key, out ZoneEdit? e))
                {
                    e.ExistsInWorld = true;
                    e.Changed = !e.SameGround(s);
                }
            }
            Version++;
        }
    }

    /// <summary>Zones the editor has put back to their saved / applied state (discarding pending changes).</summary>
    public void MarkUnchanged(IEnumerable<(int X, int Z)> zones)
    {
        lock (_lock)
        {
            foreach (var key in zones)
            {
                if (_zones.TryGetValue(key, out ZoneEdit? e))
                {
                    e.Changed = false;
                }
            }
            Version++;
        }
    }

    /// <summary>
    /// Takes a zone's edit from the page (sanitised first, and kept, not copied); it is pending while it
    /// differs from the zone's baseline.
    /// </summary>
    /// <exception cref="ArgumentException">The zone's arrays are not 65 × 65.</exception>
    public void Put(ZoneEdit incoming)
    {
        incoming.Sanitize();
        lock (_lock)
        {
            bool exists = _zones.TryGetValue((incoming.ZoneX, incoming.ZoneZ), out ZoneEdit? old);
            incoming.ExistsInWorld = exists && old!.ExistsInWorld;
            incoming.Changed = !_baseline.TryGetValue((incoming.ZoneX, incoming.ZoneZ), out ZoneEdit? baseline) ? !incoming.IsEmpty : !incoming.SameGround(baseline);
            _zones[(incoming.ZoneX, incoming.ZoneZ)] = incoming;
            Version++;
        }
    }

    /// <summary>The working zones and the object lists as they were at <see cref="Mark"/>, for <see cref="Restore"/>.</summary>
    /// <param name="Zones">Copies of the working zones.</param>
    /// <param name="Deleted">The saved objects left out.</param>
    /// <param name="Added">The objects added.</param>
    /// <param name="Trash">The objects added and deleted again.</param>
    public sealed record Checkpoint(Dictionary<(int, int), ZoneEdit> Zones, HashSet<int> Deleted, Dictionary<int, NewObject> Added, Dictionary<int, NewObject> Trash);

    /// <summary>
    /// The working zones and the object lists as they are now, to put back with <see cref="Restore"/> (zone resets,
    /// baselines and the game's ground are not in it).
    /// </summary>
    public Checkpoint Mark()
    {
        lock (_lock)
        {
            return new Checkpoint(_zones.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()), [.. _deleted], new(_added), new(_addedTrash));
        }
    }

    /// <summary>Puts the working zones and the object lists back as they were at <see cref="Mark"/>.</summary>
    public void Restore(Checkpoint mark)
    {
        lock (_lock)
        {
            _zones.Clear();
            foreach (var (key, zone) in mark.Zones)
            {
                _zones[key] = zone.Clone();
            }
            _deleted.Clear();
            _deleted.UnionWith(mark.Deleted);
            _added.Clear();
            foreach (var (id, o) in mark.Added)
            {
                _added[id] = o;
            }
            _addedTrash.Clear();
            foreach (var (id, o) in mark.Trash)
            {
                _addedTrash[id] = o;
            }
            Version++;
        }
    }
}

/// <summary>
/// One zone's terrain modifications, as the game keeps them (TerrainComp): per vertex of the 65 × 65 grid,
/// a height change and a paint, plus the editor's own lift beyond the game's limits.
/// </summary>
public sealed class ZoneEdit(int zoneX, int zoneZ)
{
    /// <summary>The zone's column (64 m zones, 0 at the world's centre).</summary>
    public int ZoneX { get; } = zoneX;

    /// <summary>The zone's row (64 m zones, 0 at the world's centre).</summary>
    public int ZoneZ { get; } = zoneZ;

    /// <summary>True when the world file already has terrain data for this zone.</summary>
    public bool ExistsInWorld { get; set; }

    /// <summary>True when the editor changed this zone since the world was loaded (or since it was last applied).</summary>
    public bool Changed { get; set; }

    /// <summary>Per vertex: its height is changed (Level and Smooth count only where it is).</summary>
    public bool[] Modified { get; init; } = new bool[EditStore.Cells];

    /// <summary>Per vertex: the level change (metres, ±8).</summary>
    public float[] Level { get; init; } = new float[EditStore.Cells];

    /// <summary>Per vertex: the smoothing change (metres, ±1).</summary>
    public float[] Smooth { get; init; } = new float[EditStore.Cells];

    /// <summary>Per vertex: its paint is changed.</summary>
    public bool[] PaintModified { get; init; } = new bool[EditStore.Cells];

    /// <summary>RGBA per vertex: r dirt, g cultivated, b paved, a vegetation allowed.</summary>
    public float[] Paint { get; init; } = new float[EditStore.Cells * 4];

    /// <summary>
    /// Height beyond the game's ±8 m (the editor's No limit): not saved as such, but turned into
    /// ground discs the game counts as generated ground (Uplift) when saving or applying.
    /// </summary>
    public float[] Lift { get; init; } = new float[EditStore.Cells];

    /// <summary>Whether any vertex has a lift.</summary>
    public bool HasLift => Lift.Any(l => l != 0f);

    /// <summary>How many vertices have their height changed.</summary>
    public int HeightCount => Modified.Count(m => m);

    /// <summary>How many vertices have their paint changed.</summary>
    public int PaintCount => PaintModified.Count(m => m);

    /// <summary>Nothing changed: no height, paint or lift.</summary>
    public bool IsEmpty => !Modified.Any(m => m) && !PaintModified.Any(m => m) && !HasLift;

    /// <summary>Same resulting ground and paint (how the height is split between level and smoothing aside).</summary>
    public bool SameGround(ZoneEdit other)
    {
        for (int i = 0; i < EditStore.Cells; i++)
        {
            float a = Modified[i] ? Level[i] + Smooth[i] : 0f, b = other.Modified[i] ? other.Level[i] + other.Smooth[i] : 0f;
            if (Math.Abs(a - b) > 1e-4f || Math.Abs(Lift[i] - other.Lift[i]) > 1e-4f || PaintModified[i] != other.PaintModified[i])
            {
                return false;
            }
            if (PaintModified[i])
            {
                for (int c = 0; c < 4; c++)
                {
                    if (Math.Abs(Paint[i * 4 + c] - other.Paint[i * 4 + c]) > 1e-4f)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    /// <summary>A deep copy (the arrays copied).</summary>
    public ZoneEdit Clone() => new(ZoneX, ZoneZ)
    {
        ExistsInWorld = ExistsInWorld,
        Changed = Changed,
        Modified = (bool[])Modified.Clone(),
        Level = (float[])Level.Clone(),
        Smooth = (float[])Smooth.Clone(),
        PaintModified = (bool[])PaintModified.Clone(),
        Paint = (float[])Paint.Clone(),
        Lift = (float[])Lift.Clone()
    };

    /// <summary>Enforce the game's limits and drop no-op modifications, whatever the browser sent.</summary>
    /// <exception cref="ArgumentException">An array is not the size of the 65 × 65 grid.</exception>
    public void Sanitize()
    {
        if (Modified.Length != EditStore.Cells || Level.Length != EditStore.Cells || Smooth.Length != EditStore.Cells || PaintModified.Length != EditStore.Cells || Paint.Length != EditStore.Cells * 4
            || Lift.Length != EditStore.Cells)
        {
            throw new ArgumentException("Zone data must have 65x65 entries.");
        }
        for (int i = 0; i < EditStore.Cells; i++)
        {
            Level[i] = float.IsFinite(Level[i]) ? Math.Clamp(Level[i], -EditStore.MaxLevel, EditStore.MaxLevel) : 0f;
            Smooth[i] = float.IsFinite(Smooth[i]) ? Math.Clamp(Smooth[i], -EditStore.MaxSmooth, EditStore.MaxSmooth) : 0f;
            Lift[i] = float.IsFinite(Lift[i]) ? Math.Clamp(Lift[i], -EditStore.MaxLift, EditStore.MaxLift) : 0f;
            if (!Modified[i])
            {
                Level[i] = 0f;
                Smooth[i] = 0f;
            }
            for (int c = 0; c < 4; c++)
            {
                float v = Paint[i * 4 + c];
                Paint[i * 4 + c] = float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : 0f;
            }
        }
    }
}

/// <summary>A new object placed in the editor.</summary>
/// <param name="Id">The negative id the browser gave it.</param>
/// <param name="Prefab">Its prefab hash.</param>
/// <param name="Position">World position (metres).</param>
/// <param name="Rotation">Unity Euler rotation (degrees).</param>
/// <param name="Scale">Uniform scale (0 = as the template object).</param>
/// <param name="SourceId">The object it comes from (else the first object of the prefab).</param>
/// <param name="Fresh">
/// A new independent object (only the builder and the scale are taken from the source, the scale unless
/// Scale gives one); otherwise, for a moved object,
/// all of the source's data (chest contents, health, builder...) is kept.
/// </param>
/// <param name="Raw">
/// The object's complete data in the save format (an object edited in the inspector, or restored from a
/// backup); it is written with this position, rotation and scale instead of copying a source.
/// </param>
/// <param name="Data">Values set on it however it is made (a copy of a model, blank, or Raw), last; see ObjectField.</param>
public sealed record NewObject(int Id, int Prefab, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale, int? SourceId = null, bool Fresh = true, byte[]? Raw = null,
    IReadOnlyList<ObjectField>? Data = null);

/// <summary>
/// A value of an object's data. As text (blueprints, the clipboard): "section;key;value in base64" ("-" for
/// none).
/// </summary>
/// <param name="Section">floats, vec3, quats, ints, longs, strings or bytes.</param>
/// <param name="Key">The stable hash of its name.</param>
/// <param name="Value">As ZdoData.Set takes it (text; base64 for bytes), null to remove it.</param>
public sealed record ObjectField(string Section, int Key, string? Value)
{
    /// <summary>The key of an object's builder ("creator").</summary>
    public static readonly int CreatorKey = StableHash.Of("creator");

    /// <summary>No builder: the game takes the object for part of a ruin (generated dungeons).</summary>
    public static readonly ObjectField NoBuilder = new("longs", CreatorKey, null);

    /// <summary>The field as text: "section;key;value in base64" ("-" for none).</summary>
    public override string ToString() => $"{Section};{Key.ToString(System.Globalization.CultureInfo.InvariantCulture)};"
        + (Value == null ? "-" : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Value)));

    /// <summary>A field from its text form; null when it is malformed or its value does not fit its section.</summary>
    public static ObjectField? Parse(string text)
    {
        string[] p = text.Split(';');
        if (p.Length != 3 || p[0] is not ("floats" or "vec3" or "quats" or "ints" or "longs" or "strings" or "bytes")
            || !int.TryParse(p[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int key))
        {
            return null;
        }
        try
        {
            string? value = p[2] == "-" ? null : System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[2]));
            // A value the save cannot take (a damaged or hand-edited line) is left out here, not when the world is written.
            return ZdoData.Valid(p[0], value) ? new ObjectField(p[0], key, value) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>Give a zone back to the world generator.</summary>
/// <param name="X">The zone's column.</param>
/// <param name="Z">The zone's row.</param>
/// <param name="KeepBuildings">Keeps player-built pieces.</param>
/// <param name="Ground">Also removes the terrain edits.</param>
public sealed record ZoneReset(int X, int Z, bool KeepBuildings, bool Ground);

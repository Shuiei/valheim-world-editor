using System.Globalization;
using System.Numerics;
using System.Text.Json;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

/// <summary>
/// An object in the running game: its ZDOID, and what it is and where, to find it again when another mod has made it
/// again under a new ZDOID.
/// </summary>
/// <param name="User">The ZDOID's user (owner peer) part.</param>
/// <param name="Id">The ZDOID's id part.</param>
/// <param name="Prefab">The object's prefab name hash.</param>
/// <param name="Position">The object's world position, in metres.</param>
public readonly record struct LiveTarget(long User, uint Id, int Prefab, Vector3 Position);

/// <summary>
/// An object to move in the running game under its own ZDOID, so what mods know of it (by its ZDO) holds.
/// </summary>
/// <param name="From">The object, where it stands now.</param>
/// <param name="Position">Its new place, in metres.</param>
/// <param name="Rotation">Its new turn, Unity Euler angles in degrees.</param>
/// <param name="Fallback">
/// The object at its new place in the save format: what the plugin makes instead (destroying the object) when a player
/// has both places loaded, whose game would not show it moving.
/// </param>
public readonly record struct LiveMove(LiveTarget From, Vector3 Position, Vector3 Rotation, byte[] Fallback);

/// <summary>
/// Keeps the running game's objects in step with the editor's deletions and new objects. It remembers what has already
/// been applied, so each apply sends only the difference, and undo works after applying: a deleted object is created
/// again from its snapshot bytes, a new one is destroyed.
/// </summary>
public sealed class LiveSync
{
    /// <summary>
    /// Guards the collections below and the unanswered call; Apply records the game's answer after an await, possibly on
    /// another thread.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>Snapshot objects (ids &gt;= 0) currently destroyed in the game.</summary>
    private readonly HashSet<int> _destroyed = [];

    /// <summary>Objects whose live ZDOID is not the snapshot's: new objects (ids &lt; 0) and restored ones.</summary>
    private readonly Dictionary<int, LiveTarget> _liveIds = [];

    /// <summary>
    /// Snapshot objects (in <see cref="_destroyed"/>) whose game object now stands for one of the editor's new objects,
    /// moved there: snapshot id to new object id (its ZDO in <see cref="_liveIds"/>). Moved back, or on, from there.
    /// </summary>
    private readonly Dictionary<int, int> _movedTo = [];

    /// <summary>Forgets everything applied, once the world was read again or its pending changes dropped.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _destroyed.Clear();
            _liveIds.Clear();
            _movedTo.Clear();
            _unanswered = null;
        }
    }

    /// <summary>Whether the game has this new object already (it was sent and accepted).</summary>
    public bool IsLive(int id)
    {
        lock (_lock)
        {
            return _liveIds.ContainsKey(id);
        }
    }

    /// <summary>Whether the game no longer has this object because the editor removed it (applied).</summary>
    public bool IsDestroyed(int id)
    {
        lock (_lock)
        {
            return _destroyed.Contains(id);
        }
    }

    /// <summary>
    /// The game removed an object the editor made (a player picked or destroyed it): no longer the editor's to remove
    /// or to count.
    /// </summary>
    public void Forget(int id)
    {
        lock (_lock)
        {
            _liveIds.Remove(id);
            foreach (int source in _movedTo.Where(m => m.Value == id).Select(m => m.Key).ToList())
            {
                _movedTo.Remove(source);
            }
        }
    }

    /// <summary>The editor's new objects the game has, by id (ids &lt; 0).</summary>
    public List<(int Id, LiveTarget Target)> NewObjects()
    {
        lock (_lock)
        {
            return [.. _liveIds.Where(kv => kv.Key < 0).Select(kv => (kv.Key, kv.Value))];
        }
    }

    /// <summary>Whether this ZDOID is one the editor made (a new object, or a removed one brought back).</summary>
    public bool IsOurs((long User, uint Id) zdo)
    {
        lock (_lock)
        {
            return _liveIds.Values.Any(t => t.User == zdo.User && t.Id == zdo.Id);
        }
    }

    /// <summary>
    /// How many object deletions and additions the next Apply would send: edits not in the game yet plus applied ones
    /// since undone.
    /// </summary>
    public (int Deleted, int Added) Pending(EditStore edits)
    {
        lock (_lock)
        {
            HashSet<int> deleted = edits.Deleted;
            var added = edits.Added.Select(a => a.Id).ToHashSet();
            int d = deleted.Count(id => id >= 0 && !_destroyed.Contains(id)) + _destroyed.Count(id => !deleted.Contains(id));
            int a = added.Count(id => !_liveIds.ContainsKey(id)) + _liveIds.Keys.Count(id => id < 0 && !added.Contains(id));
            return (d, a);
        }
    }

    /// <summary>
    /// Sends the object changes since the last apply to the game in one call, and records what the game accepted
    /// (nothing when the call fails, so it is sent again next time). New objects of kinds the editor cannot write are
    /// skipped. With a bridge that moves objects (1.40.0 on), an object moved in the editor (a saved object deleted, with
    /// one copy of it elsewhere that differs only by where it stands and how it is turned: a move, or a cut then pasted)
    /// moves in the game under its own ZDOID, so what mods know of it holds; moved back the same way on undo. With a
    /// bridge that does a call once (1.45.3 on), a call whose answer was lost (the connection dropped or timed out after
    /// the game did it) is sent again first under the same id, and the game answers with what it did instead of doing it
    /// twice; the changes since follow in a call of their own.
    /// </summary>
    /// <returns>A one-line summary for the user, or "" when there was nothing to send.</returns>
    public async Task<string> Apply(WorldSave world, EditStore edits, LiveBridge live)
    {
        List<string> said = [];
        Call? unanswered;
        lock (_lock)
        {
            unanswered = _unanswered;
        }
        if (unanswered != null)
        {
            said.Add(await Send(world, live, unanswered));
        }
        Call call = Plan(world, edits, await live.CanMove());
        if (!call.Empty)
        {
            call.Id = await live.CanCallOnce() ? Guid.NewGuid().ToString("N") : null;
            said.Add(await Send(world, live, call));
        }
        return string.Join("; ", said);
    }

    /// <summary>
    /// One call to the game's objects: what it sends, and what the game has once it accepts it (recorded only then:
    /// a refused call is sent again next time).
    /// </summary>
    private sealed class Call
    {
        /// <summary>The call's id, for a plugin that does a call once (<see cref="LiveBridge.CanCallOnce"/>); null: none.</summary>
        public string? Id { get; set; }

        /// <summary>The game objects to destroy.</summary>
        public List<LiveTarget> Destroy { get; } = [];

        /// <summary>The objects to make, in the save format.</summary>
        public List<byte[]> Create { get; } = [];

        /// <summary>The id each creation is for: a new object, or a saved one brought back.</summary>
        public List<int> CreateFor { get; } = [];

        /// <summary>What each creation is and where (for its LiveTarget once the game gives its ZDOID).</summary>
        public List<(int Prefab, Vector3 Position)> CreateWhat { get; } = [];

        /// <summary>New objects of kinds the editor cannot write, left out.</summary>
        public List<string> Skipped { get; } = [];

        /// <summary>Saved objects the call destroys.</summary>
        public List<int> DestroyIds { get; } = [];

        /// <summary>Saved objects the call makes again (deletions undone).</summary>
        public List<int> RestoreIds { get; } = [];

        /// <summary>New objects the call destroys again.</summary>
        public List<int> DropNew { get; } = [];

        /// <summary>The objects to move.</summary>
        public List<LiveMove> Moves { get; } = [];

        /// <summary>
        /// Each move's snapshot object, the id whose game object moves (that object, or a new object it was moved to)
        /// and the id it stands for once moved.
        /// </summary>
        public List<(int Source, int From, int To)> MoveFor { get; } = [];

        /// <summary>Whether there is nothing to send.</summary>
        public bool Empty => Destroy.Count + Create.Count + Moves.Count == 0;
    }

    /// <summary>
    /// The last call left without an answer (the connection dropped or timed out): the game may have done it. Sent
    /// again first, under its id; null when every call was answered.
    /// </summary>
    private Call? _unanswered;

    /// <summary>The call that brings the game to the editor's objects, from what was applied so far.</summary>
    /// <param name="world">The world the ids are of.</param>
    /// <param name="edits">The editor's deletions and new objects.</param>
    /// <param name="canMove">Whether the plugin moves objects (else a move is a destroy and a creation).</param>
    private Call Plan(WorldSave world, EditStore edits, bool canMove)
    {
        Call call = new();
        lock (_lock)
        {
            HashSet<int> deleted = edits.Deleted;
            var added = edits.Added.ToDictionary(a => a.Id);
            LiveTarget LiveId(int id)
            {
                if (_liveIds.TryGetValue(id, out var l))
                {
                    return l;
                }
                ObjectRef o = world.ObjectRefs[id];
                return new LiveTarget(o.LiveId.User, o.LiveId.Id, o.Prefab, o.Position);
            }
            // Ids a move takes care of: left out of the destroys and creations below.
            HashSet<int> moved = [];
            if (canMove)
            {
                FindMoves(world, deleted, added, LiveId, call.Moves, call.MoveFor);
                foreach (var (source, from, to) in call.MoveFor)
                {
                    moved.Add(source);
                    moved.Add(from);
                    moved.Add(to);
                }
            }
            foreach (int id in deleted.Where(id => id >= 0 && id < world.ObjectRefs.Count && !_destroyed.Contains(id) && !moved.Contains(id)))
            {
                call.Destroy.Add(LiveId(id));
                call.DestroyIds.Add(id);
            }
            // Undone deletions: the object comes back as a copy of its snapshot bytes.
            foreach (int id in _destroyed.Where(id => !deleted.Contains(id) && !moved.Contains(id)).ToList())
            {
                ObjectRef o = world.ObjectRefs[id];
                call.Create.Add(world.ObjectBytes(id));
                call.CreateFor.Add(id);
                call.CreateWhat.Add((o.Prefab, o.Position));
                call.RestoreIds.Add(id);
            }
            // New objects not in the game yet, and ones that were removed again (undo, delete).
            foreach (NewObject n in added.Values.Where(n => !_liveIds.ContainsKey(n.Id) && !moved.Contains(n.Id)))
            {
                byte[]? bytes = world.NewObjectBytes(n, m => world.LiveSource(m.File));
                if (bytes == null)
                {
                    call.Skipped.Add($"unknown kind of object at {n.Position.X:F0}, {n.Position.Z:F0}");
                    continue;
                }
                call.Create.Add(bytes);
                call.CreateFor.Add(n.Id);
                call.CreateWhat.Add((n.Prefab, n.Position));
            }
            foreach (int id in _liveIds.Keys.Where(id => id < 0 && !added.ContainsKey(id) && !moved.Contains(id)).ToList())
            {
                call.Destroy.Add(_liveIds[id]);
                call.DropNew.Add(id);
            }
        }
        return call;
    }

    /// <summary>
    /// Sends a call and records what the game did. A call with an id left without an answer is kept, to be sent again
    /// first next time; one the plugin answered with an error was not done, and is planned again.
    /// </summary>
    /// <returns>A one-line summary for the user.</returns>
    private async Task<string> Send(WorldSave world, LiveBridge live, Call call)
    {
        string reply;
        try
        {
            reply = await live.ApplyObjects(call.Destroy, call.Create, call.Moves, call.Id);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            lock (_lock)
            {
                _unanswered = call.Id != null ? call : null;
            }
            throw;
        }
        catch (InvalidOperationException)
        {
            lock (_lock)
            {
                _unanswered = null;
            }
            throw;
        }
        lock (_lock)
        {
            _unanswered = null;
        }
        return Record(world, call, reply);
    }

    /// <summary>What the game did with a call, from its answer: recorded, and told in one line for the user.</summary>
    /// <param name="world">The world the ids are of.</param>
    /// <param name="call">The call answered.</param>
    /// <param name="reply">The plugin's JSON answer.</param>
    private string Record(WorldSave world, Call call, string reply)
    {
        using var doc = JsonDocument.Parse(reply);
        var ids = doc.RootElement.GetProperty("created").EnumerateArray().Select(e => e.GetString()!.Split(':')).ToList();
        var moveReplies = doc.RootElement.TryGetProperty("moves", out var mv)
            ? mv.EnumerateArray().Select(e => (Id: e.GetProperty("id").GetString()!.Split(':'), InPlace: e.GetProperty("inPlace").GetBoolean())).ToList()
            : [];
        // Snapshot objects moved back, now followed under the ZDOID that came back.
        List<(int Id, (long User, uint Id) Zdo)> movedBack = [];
        lock (_lock)
        {
            foreach (int id in call.DestroyIds)
            {
                _destroyed.Add(id);
                _liveIds.Remove(id);
            }
            foreach (int id in call.RestoreIds)
            {
                _destroyed.Remove(id);
                // Made again on its own: a copy it was moved to is a copy of its own from now on.
                _movedTo.Remove(id);
            }
            foreach (int id in call.DropNew)
            {
                _liveIds.Remove(id);
                foreach (int source in _movedTo.Where(m => m.Value == id).Select(m => m.Key).ToList())
                {
                    _movedTo.Remove(source);
                }
            }
            for (int i = 0; i < ids.Count && i < call.CreateFor.Count; i++)
            {
                _liveIds[call.CreateFor[i]] = new LiveTarget(long.Parse(ids[i][0], CultureInfo.InvariantCulture), uint.Parse(ids[i][1], CultureInfo.InvariantCulture), call.CreateWhat[i].Prefab, call.CreateWhat[i].Position);
            }
            for (int i = 0; i < moveReplies.Count && i < call.MoveFor.Count; i++)
            {
                var (source, from, to) = call.MoveFor[i];
                var zdo = (long.Parse(moveReplies[i].Id[0], CultureInfo.InvariantCulture), uint.Parse(moveReplies[i].Id[1], CultureInfo.InvariantCulture));
                var target = new LiveTarget(zdo.Item1, zdo.Item2, world.ObjectRefs[source].Prefab, call.Moves[i].Position);
                _liveIds.Remove(from);
                if (to == source)
                {
                    _destroyed.Remove(source);
                    _movedTo.Remove(source);
                    _liveIds[source] = target;
                    movedBack.Add((source, zdo));
                }
                else
                {
                    _destroyed.Add(source);
                    _movedTo[source] = to;
                    _liveIds[to] = target;
                }
            }
        }
        // Objects of the world made again in the game (an applied delete undone): followed under their new
        // ZDOID from now on (WorldSave.MergeLive), as the world's own.
        for (int i = 0; i < ids.Count && i < call.CreateFor.Count; i++)
        {
            if (call.CreateFor[i] >= 0)
            {
                world.SetLiveId(call.CreateFor[i], (long.Parse(ids[i][0], CultureInfo.InvariantCulture), uint.Parse(ids[i][1], CultureInfo.InvariantCulture)));
            }
        }
        foreach (var (id, zdo) in movedBack)
        {
            if (world.ObjectRefs[id].LiveId != zdo)
            {
                world.SetLiveId(id, zdo);
            }
        }
        int destroyed = doc.RootElement.GetProperty("destroyed").GetInt32(), missing = doc.RootElement.GetProperty("missing").GetInt32();
        string msg = $"{destroyed} object(s) removed, {ids.Count} created";
        if (moveReplies.Count > 0)
        {
            int inPlace = moveReplies.Count(m => m.InPlace);
            msg += $", {inPlace} moved";
            if (inPlace < moveReplies.Count)
            {
                msg += $" ({moveReplies.Count - inPlace} more made again at their new place: a player near them would not see them move)";
            }
        }
        if (doc.RootElement.TryGetProperty("refound", out var rf) && rf.GetInt32() is > 0 and int refound)
        {
            msg += $" ({refound} made again by another mod, found at their place)";
        }
        if (missing > 0)
        {
            msg += $", {missing} were already gone";
        }
        if (call.Skipped.Count > 0)
        {
            msg += $", {call.Skipped.Count} skipped ({call.Skipped[0]})";
        }
        return msg;
    }

    /// <summary>
    /// The moves the next apply sends: for each snapshot object, the game object standing for it (its own, or the one
    /// it was moved to before) goes where the editor has it now, when that is somewhere else and the object is the
    /// same otherwise. Back to its own place when the object is no longer deleted (an undo); else to the first new
    /// object that is a copy of it (<see cref="NewObject.SourceId"/>) differing only by place and turn, not yet in the
    /// game.
    /// </summary>
    /// <param name="world">The world the ids are of.</param>
    /// <param name="deleted">The editor's deleted snapshot objects.</param>
    /// <param name="added">The editor's new objects, by id.</param>
    /// <param name="liveId">The game object of a snapshot object.</param>
    /// <param name="moves">Where the moves go.</param>
    /// <param name="moveFor">Where each move's snapshot object, the id it moves from and the one it moves to go.</param>
    private void FindMoves(WorldSave world, HashSet<int> deleted, Dictionary<int, NewObject> added, Func<int, LiveTarget> liveId,
        List<LiveMove> moves, List<(int Source, int From, int To)> moveFor)
    {
        // Copies of saved objects not in the game yet, by the object they copy, in the editor's order.
        var copies = added.Values.Where(n => n.SourceId is int s && s >= 0 && s < world.ObjectRefs.Count && n.Raw == null && !_liveIds.ContainsKey(n.Id))
            .GroupBy(n => n.SourceId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var sources = deleted.Where(id => id >= 0 && id < world.ObjectRefs.Count).Concat(_movedTo.Keys).ToHashSet();
        foreach (int source in sources)
        {
            int from = _movedTo.TryGetValue(source, out int to0) ? to0 : _destroyed.Contains(source) ? int.MinValue : source;
            // Destroyed for good, or still where it stands: in the game as the editor has it.
            if (from == int.MinValue || from < 0 && added.ContainsKey(from) || from == source && !deleted.Contains(source))
            {
                continue;
            }
            // Linked objects (a ship and its parts) are rewritten as groups when copied: made again as they are.
            if (world.Links.ContainsKey(source))
            {
                continue;
            }
            ObjectRef o = world.ObjectRefs[source];
            byte[] own = world.ObjectBytes(source);
            var original = ZdoData.Parse(own, o.File.WorldVersion);
            LiveTarget at = from == source ? liveId(source) : _liveIds[from];
            if (!deleted.Contains(source))
            {
                moves.Add(new LiveMove(at, o.Position, original.Rotation, own));
                moveFor.Add((source, from, source));
                continue;
            }
            if (!copies.TryGetValue(source, out var list))
            {
                continue;
            }
            foreach (NewObject n in list)
            {
                if (n.Prefab != o.Prefab || world.NewObjectBytes(n, m => world.LiveSource(m.File)) is not { } bytes)
                {
                    continue;
                }
                var copy = ZdoData.Parse(bytes, o.File.WorldVersion);
                if (!SameButPlace(original, copy))
                {
                    continue;
                }
                moves.Add(new LiveMove(at, n.Position, n.Rotation, bytes));
                moveFor.Add((source, from, n.Id));
                list.Remove(n);
                break;
            }
        }
    }

    /// <summary>
    /// Whether two objects are the same but for where they stand and how they are turned: prefab, flags, connection
    /// and every value (in any order).
    /// </summary>
    private static bool SameButPlace(ZdoData a, ZdoData b)
    {
        static bool Same<T>(List<(int Key, T Value)> x, List<(int Key, T Value)> y, Func<T, T, bool> eq)
        {
            if (x.Count != y.Count)
            {
                return false;
            }
            var byKey = new Dictionary<int, T>(x.Count);
            foreach (var (k, v) in x)
            {
                byKey[k] = v;
            }
            return y.All(e => byKey.TryGetValue(e.Key, out T? v) && eq(v, e.Value));
        }
        return a.Prefab == b.Prefab && a.BaseFlags == b.BaseFlags
            && (a.Connection ?? []).AsSpan().SequenceEqual(b.Connection ?? [])
            && Same(a.FloatList, b.FloatList, (p, q) => p.Equals(q))
            && Same(a.Vec3List, b.Vec3List, (p, q) => p.Equals(q))
            && Same(a.QuatList, b.QuatList, (p, q) => p.Equals(q))
            && Same(a.IntList, b.IntList, (p, q) => p == q)
            && Same(a.LongList, b.LongList, (p, q) => p == q)
            && Same(a.StringList, b.StringList, string.Equals)
            && Same(a.ByteList, b.ByteList, (p, q) => p.AsSpan().SequenceEqual(q));
    }
}

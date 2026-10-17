using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
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
            LiveTarget at = from == source ? liveId(source) : _liveIds[from];
            // Still deleted with no copy to move to: nothing moves, so its bytes are not read. Not deleted any more
            // (list stays null): it moves back.
            List<NewObject>? list = null;
            if (deleted.Contains(source) && !copies.TryGetValue(source, out list))
            {
                continue;
            }
            byte[] own = world.ObjectBytes(source);
            SourceReads++;
            var original = ZdoData.Parse(own, o.File.WorldVersion);
            if (list == null)
            {
                moves.Add(new LiveMove(at, o.Position, original.Rotation, own));
                moveFor.Add((source, from, source));
                continue;
            }
            foreach (NewObject n in list)
            {
                if (n.Prefab != o.Prefab || world.NewObjectBytes(n, world.SourceReader()) is not { } bytes)
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

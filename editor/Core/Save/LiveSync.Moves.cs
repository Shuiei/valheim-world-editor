using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>
    /// The moves the next apply sends: for each snapshot object, the game object standing for it (its own, or the one
    /// it was moved to before) goes where the editor has it now, when that is somewhere else and the object is the
    /// same otherwise. Back to its own place when the object is no longer deleted (an undo); else to the first new
    /// object that is a copy of it (<see cref="NewObject.SourceId"/>, or of the copy it was moved to) differing only by
    /// place and turn, not yet in the game. With <paramref name="canUpdate"/>, a copy whose data differs too is moved to
    /// with its data (and moved back with the object's own).
    /// </summary>
    /// <param name="world">The world the ids are of.</param>
    /// <param name="deleted">The editor's deleted snapshot objects.</param>
    /// <param name="added">The editor's new objects, by id.</param>
    /// <param name="liveId">The game object of a snapshot object.</param>
    /// <param name="canUpdate">Whether the plugin changes data in place.</param>
    /// <param name="moves">Where the moves go.</param>
    /// <param name="moveFor">Where each move's snapshot object, the id it moves from and the one it moves to go.</param>
    private void FindMoves(WorldSave world, HashSet<int> deleted, Dictionary<int, NewObject> added, Func<int, LiveTarget> liveId,
        bool canUpdate, List<LiveMove> moves, List<(int Source, int From, int To)> moveFor)
    {
        var copies = CopiesOf(world, added, canUpdate);
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
                moves.Add(new LiveMove(at, o.Position, original.Rotation, own, _dataChanged.Contains(source)));
                moveFor.Add((source, from, source));
                continue;
            }
            MoveToCopy(world, list, o, original, at, (source, from), canUpdate, moves, moveFor);
        }
    }

    /// <summary>
    /// The editor's copies of saved objects not in the game yet, by the object they copy (a copy of the copy one was
    /// moved to: by that one's), in the editor's order. Ones carrying their own bytes only for a plugin that changes data.
    /// </summary>
    private Dictionary<int, List<NewObject>> CopiesOf(WorldSave world, Dictionary<int, NewObject> added, bool canUpdate)
    {
        var movedFrom = _movedTo.ToDictionary(m => m.Value, m => m.Key);
        int? Copied(NewObject n) => n.SourceId is not int s ? null : s >= 0 ? s : movedFrom.TryGetValue(s, out int source) ? source : null;
        return added.Values.Where(n => Copied(n) is int s && s < world.ObjectRefs.Count && (n.Raw == null || canUpdate) && !_liveIds.ContainsKey(n.Id))
            .GroupBy(n => Copied(n)!.Value).ToDictionary(g => g.Key, g => g.ToList());
    }
}

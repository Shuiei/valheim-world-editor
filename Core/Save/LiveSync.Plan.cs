using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
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
                byte[]? bytes = world.NewObjectBytes(n, world.SourceReader());
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
}

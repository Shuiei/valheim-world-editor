using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>
    /// The object changes since the save read: deletions, moves (a deletion and a copy keeping the object's data),
    /// other additions, and what the editor's own save wrote and was changed since.
    /// </summary>
    private void PlanObjects(HashSet<int> deleted, List<NewObject> added, List<NewObject> gone, Saved saved)
    {
        var moves = added.Where(n => !n.Fresh && n.SourceId is int s && s >= 0 && deleted.Contains(s))
            .GroupBy(n => n.SourceId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var moved = moves.Values.SelectMany(m => m).ToHashSet();
        var current = added.ToHashSet();
        Contest(deleted, saved);
        // Objects added in place of one of the save read, or of one the editor's save wrote (an edited or claimed copy
        // where the original stood): settled with that one, not on their own.
        var attached = new HashSet<NewObject>();
        var pending = added.Where(n => !moved.Contains(n) && !InGameSave(n)).ToList();
        var inPlace = new Dictionary<(int, long, long, long), Queue<NewObject>>();
        foreach (var n in pending.Where(n => n.SourceId == null).OrderByDescending(n => n.Id))
        {
            var key = ObjectPlaces.Key(n.Prefab, n.Position);
            if (!inPlace.TryGetValue(key, out var q))
            {
                inPlace[key] = q = new Queue<NewObject>();
            }
            q.Enqueue(n);
        }
        List<NewObject> Replacing(int prefab, Vector3 position)
        {
            List<NewObject> with = inPlace.TryGetValue(ObjectPlaces.Key(prefab, position), out var q) && q.Count > 0 ? [q.Dequeue()] : [];
            attached.UnionWith(with);
            return with;
        }
        // Deleted (an object put in its place with it): gone from the game's save too (the game removed it, or the
        // editor's save did), nothing to delete.
        foreach (int s in deleted.Where(s => !moves.ContainsKey(s)).Order())
        {
            var o = _old.ObjectRefs[s];
            var with = Replacing(o.Prefab, o.Position);
            Settle(s, o.Prefab, o.Position, Find(s), y => SameData(s, y), with, goneSaysNothing: saved.Deleted.Contains(s));
        }
        // Deleted, saved, then undone: back, unless the game's save has it (made from the save the game had).
        foreach (int s in saved.Deleted.Where(s => !deleted.Contains(s)).Order())
        {
            if (Find(s) == null)
            {
                _comeBack.Add(s);
            }
        }
        // Added, saved, then deleted or changed (changed: its new version in its place): as a deletion of what the
        // editor's save wrote.
        // A copy moved from it (the same object but for its place, turn and size; one only, else none is taken for
        // its move) goes with it too, taking the game's object along when the move is kept.
        foreach (var n in saved.Added.Where(n => !current.Contains(n)).OrderByDescending(n => n.Id))
        {
            var with = Replacing(n.Prefab, n.Position);
            var copies = added.Where(a => a.Id != n.Id && !attached.Contains(a) && !InGameSave(a)
                && a with { Id = n.Id, Position = n.Position, Rotation = n.Rotation, Scale = n.Scale } == n).Take(2).ToList();
            List<NewObject> moving = copies.Count == 1 ? copies : [];
            attached.UnionWith(moving);
            Settle(n.Id, n.Prefab, n.Position, FindSaved(n.Id), y => WrittenBy(n) is { } was && NewData(y) is { } now ? was.AsSpan().SequenceEqual(now) : null, with, goneSaysNothing: true, moving);
        }
        foreach (var n in pending.Where(n => !attached.Contains(n)))
        {
            _adds.Add(Remapped(n));
        }
        foreach (var (s, copies) in moves.OrderBy(m => m.Key))
        {
            var left = copies.Where(n => !InGameSave(n) && !attached.Contains(n)).ToList();
            if (left.Count == 0)
            {
                continue;
            }
            if (Find(s) is int y)
            {
                List<NewObject> mine = [.. left.Select(n => n with { SourceId = y })];
                if (SameData(s, y) is true)
                {
                    _adds.AddRange(mine);
                    _deletes.Add(y);
                }
                else
                {
                    Clashing(new Item(ReplayClash.Changed, s, y, mine, left));
                }
            }
            else if (saved.Deleted.Contains(s))
            {
                // Moved, saved, and moved again: the editor's save removed it, not the game.
                _adds.AddRange(left.Select(n => Restored(n, s)));
            }
            else
            {
                Clashing(new Item(ReplayClash.Removed, s, null, [.. left.Select(n => Restored(n, s))], left));
            }
        }
        _gone.AddRange(gone.Select(Remapped));
        // Added and saved, then deleted or changed, and not in the game's save: kept for the history (undo or redo can
        // bring it back).
        var goneIds = gone.Select(n => n.Id).ToHashSet();
        _gone.AddRange(saved.Added.Where(n => !current.Contains(n) && FindSaved(n.Id) == null && !goneIds.Contains(n.Id)).Select(Remapped));
    }

    /// <summary>
    /// An object the editor deleted, with what it put in its place: deleted (and replaced) when the game has it as the
    /// editor knew it; a clash when the game changed it, or removed it while the editor put another in its place.
    /// </summary>
    /// <param name="old">The object as the editor knew it (an id of the save read, or an added object's own id).</param>
    /// <param name="prefab">Its prefab hash.</param>
    /// <param name="position">Where it stood.</param>
    /// <param name="newer">The object in the game's save (null: not there).</param>
    /// <param name="same">Whether the game's object has the data the editor knew (null: cannot tell).</param>
    /// <param name="with">What the editor put in its place.</param>
    /// <param name="goneSaysNothing">
    /// Its absence from the game's save is no clash: the editor's own save removed it, or wrote it (the game may have
    /// saved from the save it had).
    /// </param>
    /// <param name="moving">Copies of it moved elsewhere: kept, they take the game's object along (unless they carry data of their own).</param>
    private void Settle(int old, int prefab, Vector3 position, int? newer, Func<int, bool?> same, List<NewObject> with, bool goneSaysNothing, List<NewObject>? moving = null)
    {
        List<NewObject> moved = moving ?? [];
        List<NewObject> mine = [.. with.Select(Remapped), .. moved.Select(m => newer is int g && m.Raw == null ? m with { SourceId = g, Fresh = false } : Remapped(m))];
        List<NewObject> original = [.. with, .. moved];
        if (newer is int y)
        {
            bool? unchanged = same(y);
            if (unchanged is true)
            {
                _deletes.Add(y);
                _adds.AddRange(mine);
            }
            else
            {
                Clashing(new Item(ReplayClash.Changed, old, y, mine, original), prefab, position, unchanged == null);
            }
        }
        else if (original.Count > 0 && !goneSaysNothing)
        {
            Clashing(new Item(ReplayClash.Removed, old, null, mine, original), prefab, position, false);
        }
        else
        {
            _adds.AddRange(mine);
        }
    }
}

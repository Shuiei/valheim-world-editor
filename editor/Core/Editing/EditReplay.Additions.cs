namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>
    /// Added, saved, then deleted or changed (changed: its new version in its place): settled as a deletion of what the
    /// editor's save wrote. A copy moved from it (the same object but for its place, turn and size; one only, else none
    /// is taken for its move) goes with it too, taking the game's object along when the move is kept.
    /// </summary>
    private void SavedAdditions(List<NewObject> added, HashSet<NewObject> current, Saved saved, InPlaceAdds swaps)
    {
        foreach (var n in saved.Added.Where(n => !current.Contains(n)).OrderByDescending(n => n.Id))
        {
            var with = swaps.Take(n.Prefab, n.Position);
            var copies = added.Where(a => a.Id != n.Id && !swaps.Attached.Contains(a) && !InGameSave(a)
                && a with { Id = n.Id, Position = n.Position, Rotation = n.Rotation, Scale = n.Scale } == n).Take(2).ToList();
            List<NewObject> moving = copies.Count == 1 ? copies : [];
            swaps.Attached.UnionWith(moving);
            Settle(n.Id, n.Prefab, n.Position, FindSaved(n.Id), y => WrittenBy(n) is { } was && NewData(y) is { } now ? was.AsSpan().SequenceEqual(now) : null, with, goneSaysNothing: true, moving);
        }
    }

    /// <summary>
    /// Settles each object of the save read that was moved: a move whose source the game still has clashes or is
    /// applied, and one whose source the editor's save removed is restored.
    /// </summary>
    private void MovedCopies(Dictionary<int, List<NewObject>> moves, Saved saved, InPlaceAdds swaps)
    {
        foreach (var (s, copies) in moves.OrderBy(m => m.Key))
        {
            var left = copies.Where(n => !InGameSave(n) && !swaps.Attached.Contains(n)).ToList();
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
    }
}

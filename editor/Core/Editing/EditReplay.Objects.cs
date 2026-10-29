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
        var pending = added.Where(n => !moved.Contains(n) && !InGameSave(n)).ToList();
        var swaps = new InPlaceAdds(pending);
        DeletedObjects(deleted, moves, saved, swaps);
        UndoneDeletions(deleted, saved);
        SavedAdditions(added, current, saved, swaps);
        foreach (var n in pending.Where(n => !swaps.Attached.Contains(n)))
        {
            _adds.Add(Remapped(n));
        }
        MovedCopies(moves, saved, swaps);
        _gone.AddRange(gone.Select(Remapped));
        // Added and saved, then deleted or changed, and not in the game's save: kept for the history (undo or redo can
        // bring it back).
        var goneIds = gone.Select(n => n.Id).ToHashSet();
        _gone.AddRange(saved.Added.Where(n => !current.Contains(n) && FindSaved(n.Id) == null && !goneIds.Contains(n.Id)).Select(Remapped));
    }

    /// <summary>
    /// Settles the objects of the save read that the editor deleted (not those it moved), each with the object put in
    /// its place.
    /// </summary>
    private void DeletedObjects(HashSet<int> deleted, Dictionary<int, List<NewObject>> moves, Saved saved, InPlaceAdds swaps)
    {
        // Deleted (an object put in its place with it): gone from the game's save too (the game removed it, or the
        // editor's save did), nothing to delete.
        foreach (int s in deleted.Where(s => !moves.ContainsKey(s)).Order())
        {
            var o = _old.ObjectRefs[s];
            var with = swaps.Take(o.Prefab, o.Position);
            Settle(s, o.Prefab, o.Position, Find(s), y => SameData(s, y), with, goneSaysNothing: saved.Deleted.Contains(s));
        }
    }

    /// <summary>
    /// Deleted, saved, then undone: the object of the save read comes back, unless the game's save has it (made from the
    /// save the game had).
    /// </summary>
    private void UndoneDeletions(HashSet<int> deleted, Saved saved)
    {
        foreach (int s in saved.Deleted.Where(s => !deleted.Contains(s)).Order())
        {
            if (Find(s) == null)
            {
                _comeBack.Add(s);
            }
        }
    }
}

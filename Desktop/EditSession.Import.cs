
namespace TerrainEditor.Desktop;

/// <summary>
/// The kept history fitted into the open area (Import), in the order the steps were made.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// The kept history fitted to this area: a change whose ground and objects are all in it can be undone here; one
    /// made wholly elsewhere is kept aside (Shelved) and goes back into the world's history as it came; one partly in
    /// the area, and every change before it, is kept aside too, as nothing older could be undone under it. Objects a
    /// fitted change needs that the area does not show (deleted, or added and undone) come back as gone things.
    /// </summary>
    /// <param name="k">The kept history.</param>
    /// <param name="find">Finds things of the world by id (those this area does not have).</param>
    /// <returns>How many were kept aside.</returns>
    public int Import(Kept k, Func<IReadOnlyCollection<int>, Dictionary<int, WorldScene.Thing>> find)
    {
        lock (_lock)
        {
            _shelvedUndo.Clear();
            _shelvedRedo.Clear();
            _order.Clear();
            var index = new Dictionary<int, int>();
            for (int i = 0; i < Scene.Things.Count; i++)
            {
                index.TryAdd(Scene.Things[i].Id, i);
            }
            var missing = k.Ids.Values.SelectMany(v => v).Where(id => !index.ContainsKey(id)).Distinct().ToList();
            var found = missing.Count > 0 ? find(missing) : [];
            var fit = new KeptFit(this, k, index, found);
            // Oldest first, so a removal finds the change it took out. A straddling change takes every change before
            // it aside with it; after the last one, those that fit are undoable here and the others kept aside.
            var undo = k.Undo.Select(fit.Fit).ToList();
            int start = undo.FindLastIndex(u => u.Standing == Standing.Straddles) + 1;
            _undo.Clear();
            _shelvedUndo.AddRange(k.Undo.Take(start).Select(fit.Aside));
            for (int n = start; n < undo.Count; n++)
            {
                if (undo[n].Fitted is { } fitted)
                {
                    _undo.Add(fitted);
                }
                else
                {
                    _shelvedUndo.Add(fit.Aside(k.Undo[n]));
                }
            }
            // The redo list has the next redo last; fitted oldest first too. A straddling change takes every change
            // after it (earlier in the list) aside with it: they could only be redone after it.
            var redo = Enumerable.Reverse(k.Redo).Select(fit.Fit).Reverse().ToList();
            int end = redo.FindLastIndex(u => u.Standing == Standing.Straddles);
            _redo.Clear();
            _shelvedRedo.AddRange(k.Redo.Take(end + 1).Select(fit.Aside));
            for (int n = end + 1; n < redo.Count; n++)
            {
                if (redo[n].Fitted is { } fitted)
                {
                    _redo.Add(fitted);
                }
                else
                {
                    _shelvedRedo.Add(fit.Aside(k.Redo[n]));
                }
            }
            RestoreOrder(k, undo.Count, redo.Count, start, end, undo, redo);
            // The things found elsewhere that a step kept here refers to join the scene; those only steps kept aside
            // refer to stay out (nothing here would show or undo them).
            JoinFound([.. _undo.Concat(_redo)], fit);
            return _shelvedUndo.Count + _shelvedRedo.Count;
        }
    }

    /// <summary>
    /// Puts the imported steps in the order they were made, as the kept history has it: the undo list oldest first, then
    /// the redo list from its last (the next redo) to its first; a fitted step under its copy, one kept aside as it came.
    /// Under the lock.
    /// </summary>
    internal void RestoreOrder(Kept k, int undoCount, int redoCount, int start, int end,
        List<(Standing Standing, Change? Fitted)> undo, List<(Standing Standing, Change? Fitted)> redo)
    {
        for (int n = 0; n < undoCount; n++)
        {
            _order[n >= start && undo[n].Fitted is { } f ? f : k.Undo[n]] = n;
        }
        for (int n = 0; n < redoCount; n++)
        {
            _order[n > end && redo[n].Fitted is { } f ? f : k.Redo[n]] = undoCount + redoCount - 1 - n;
        }
        _nextOrder = undoCount + redoCount;
    }

    /// <summary>
    /// Joins the things found elsewhere that the kept steps refer to into the scene's things, and points the steps at
    /// them. Under the scene's things lock.
    /// </summary>
    /// <param name="kept">The steps kept here (the undo and redo lists).</param>
    /// <param name="fit">The fitting of the kept history, with the things found.</param>
    internal void JoinFound(List<Change> kept, KeptFit fit)
    {
        var joined = new Dictionary<int, int>();
        lock (Scene.Things)
        {
            foreach (int at in kept.SelectMany(c => c.Things).Select(t => t.Index).Where(i => i >= fit.First).Distinct().Order())
            {
                Scene.Things.Add(fit.Pending[at - fit.First]);
                joined[at] = Scene.Things.Count - 1;
            }
        }
        foreach (var c in kept)
        {
            for (int n = 0; n < c.Things.Length; n++)
            {
                if (c.Things[n].Index >= fit.First)
                {
                    c.Things[n].Index = joined[c.Things[n].Index];
                }
            }
        }
    }
}

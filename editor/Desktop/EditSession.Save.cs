using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Saving the history's changes into the world, and marking the steps saved or applied.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Writes the changes into the world's files (WorldSession.Save); the history stays, its steps saved (an undo is
    /// then pending, saved by the next save), as with Apply live, No limit ground included. After zone resets the area
    /// is read again, and its history with it.
    /// </summary>
    /// <returns>What the writer did.</returns>
    public WorldWriter.Result Save() => SaveOutcome().Saved!;

    /// <summary>As <see cref="Save"/>, with what to tell the user in both languages (the status bar's English is Claude's).</summary>
    /// <returns>The world's outcome: the writer's result in <see cref="WorldSession.Outcome.Saved"/>.</returns>
    public WorldSession.Outcome SaveOutcome()
    {
        WorldSession.Outcome o;
        WorldWriter.Result result;
        bool reread;
        lock (_lock)
        {
            if (_stroke != null)
            {
                throw new InvalidOperationException("A stroke is still going on.");
            }
            // Every area opened in the app belongs to an open world (WorldScene.Load), which saves.
            var owner = Scene.Owner ?? throw new InvalidOperationException("This area is not part of an open world, so it cannot be saved.");
            o = owner.Save();
            result = o.Saved!;
            // Read again only when the world was (zone resets) or No limit ground stayed ground discs in the
            // session (a failed save, or edits changed while saving): the ground under the area is not the same.
            // Otherwise, as after Apply live, the area and its history stay; the steps are saved.
            reread = o.Reloaded || o.Lifted;
            if (reread)
            {
                Reread(owner.World);
            }
            else if (result.Saved)
            {
                Ground.TakeEdits(Edits);
                MarkApplied(_undo);
            }
        }
        if (reread)
        {
            ThingsReset?.Invoke();
        }
        Changed?.Invoke();
        return o;
    }

    /// <summary>
    /// Marks these steps, and those kept aside from other areas, as saved or applied: the world's pending changes are
    /// one set, so a save or an apply from any area takes the steps of every area with it (and Discard must not undo
    /// them where they were made). Under the lock.
    /// </summary>
    /// <param name="steps">This area's steps.</param>
    /// <param name="sent">After a partial Apply live, whether the game has all a step did (null: every step).</param>
    private void MarkApplied(IEnumerable<Change> steps, Func<Change, bool>? sent = null)
    {
        var all = steps.Concat(_shelvedUndo.Select(s => s.Change)).ToList();
        if (sent == null)
        {
            foreach (var c in all)
            {
                c.Applied = true;
            }
            // What was undone is not in what was just written (unless undone while it was written): redone, it waits
            // again, and Discard leaves it undone.
            foreach (var c in _redo.Concat(_shelvedRedo.Select(s => s.Change)).Except(all))
            {
                c.Applied = false;
            }
            return;
        }
        // Partly sent: in the order the steps were made, this area's and the kept-aside ones together.
        MarkSent([.. all.OrderBy(OrderOf)], sent);
    }

    /// <summary>
    /// Marks steps as applied, oldest first, while the game has all they did; the first one it lacks (objects or
    /// zone resets that did not go in) stops it, so no step after it is applied either (Discard undoes from the end).
    /// </summary>
    /// <param name="steps">The steps, oldest first.</param>
    /// <param name="sent">Whether the game has all a step did.</param>
    internal static void MarkSent(IEnumerable<Change> steps, Func<Change, bool> sent)
    {
        foreach (var c in steps)
        {
            if (!c.Applied && !sent(c))
            {
                break;
            }
            c.Applied = true;
        }
    }
}

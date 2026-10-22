
namespace TerrainEditor.Desktop;

/// <summary>
/// The editor's Discard: undoing the steps not yet saved or applied.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// The editor's Discard: undoes those steps, newest first, redoes the saved (or applied) steps undone since, which
    /// are waiting too, and drops what could be redone after them, here and in other areas (kept aside); the rest of
    /// the history stays.
    /// </summary>
    /// <returns>How many were undone (none during a stroke).</returns>
    public int UndoUnapplied()
    {
        int n = 0;
        while (Next(_undo, applied: false) && Undo())
        {
            n++;
        }
        bool redone = true;
        while (redone)
        {
            // A saved step undone: discarding that undo puts it back. It was dropped with the redo list, leaving its
            // undo waiting with nothing to redo it.
            redone = Next(_redo, applied: true) && Redo();
        }
        lock (_lock)
        {
            if (_stroke == null)
            {
                _redo.Clear();
                _shelvedRedo.Clear();
            }
        }
        Changed?.Invoke();
        return n;
    }

    /// <summary>Whether the list's next step (its last) is saved or applied as asked, outside a stroke; under the lock.</summary>
    /// <param name="list">The undo or the redo list.</param>
    /// <param name="applied">The state the step must be in.</param>
    private bool Next(List<Change> list, bool applied)
    {
        lock (_lock)
        {
            return _stroke == null && list.Count > 0 && list[^1].Applied == applied;
        }
    }
}

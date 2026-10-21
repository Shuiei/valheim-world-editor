
namespace TerrainEditor.Desktop;

/// <summary>
/// The editor's Discard: undoing the steps not yet saved or applied.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// The editor's Discard: undoes those steps, newest first, and drops what could be redone, here and in other areas
    /// (kept aside); the rest of the history stays.
    /// </summary>
    /// <returns>How many were undone (none during a stroke).</returns>
    public int UndoUnapplied()
    {
        int n = 0;
        while (true)
        {
            lock (_lock)
            {
                if (_stroke != null || _undo.Count == 0 || _undo[^1].Applied)
                {
                    break;
                }
            }
            if (!Undo())
            {
                break;
            }
            n++;
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
}


namespace TerrainEditor.Desktop;

/// <summary>
/// The status the window shows: what can be undone and redone, whether a stroke goes on, what waits to be saved.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Whether there is a step to undo.
    /// </summary>
    public bool CanUndo { get { lock (_lock) { return _undo.Count > 0; } } }

    /// <summary>
    /// Whether there is a step to redo.
    /// </summary>
    public bool CanRedo { get { lock (_lock) { return _redo.Count > 0; } } }

    /// <summary>
    /// The label of the next undo; null when there is none.
    /// </summary>
    public string? UndoLabel { get { lock (_lock) { return _undo.Count > 0 ? _undo[^1].Label : null; } } }

    /// <summary>
    /// The label of the next redo; null when there is none.
    /// </summary>
    public string? RedoLabel { get { lock (_lock) { return _redo.Count > 0 ? _redo[^1].Label : null; } } }

    /// <summary>
    /// Whether a brush stroke is going on.
    /// </summary>
    public bool Stroking { get { lock (_lock) { return _stroke != null; } } }

    /// <summary>
    /// What is waiting to be saved (or applied live), like the web editor's line under the Save button: the open
    /// world's counts when there is one, else the edit store's.
    /// </summary>
    public (int Zones, int Deleted, int Added, int Resets) Pending => Scene.Owner?.Pending ?? (Edits.ChangedZoneCount, Edits.DeletedCount, Edits.AddedCount, Edits.ResetCount);

    /// <summary>
    /// Whether the area belongs to a world followed live in the running game (Apply live rather than Save).
    /// </summary>
    public bool IsLive => Scene.Owner?.IsLive == true;

    /// <summary>
    /// The steps at the end of the history not saved (offline) or applied (live) yet.
    /// </summary>
    public int UnappliedSteps
    {
        get
        {
            lock (_lock)
            {
                int n = 0;
                for (int i = _undo.Count - 1; i >= 0 && !_undo[i].Applied; i--)
                {
                    n++;
                }
                return n;
            }
        }
    }
}


namespace TerrainEditor.Desktop;

/// <summary>
/// Undo and redo: moving the last change of one list to the other, and going back to or forward to a step.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Undoes every change made after this one; nothing when it is no longer on the undo list (undone or dropped since it
    /// was picked, by Claude or the history's length).
    /// </summary>
    public void BackTo(Change c)
    {
        if (!Holds(_undo, c))
        {
            return;
        }
        // The edit told once for all the steps (one refresh, one auto-apply).
        bool any = false;
        while (Last(_undo) is { } last && !ReferenceEquals(last, c) && Step(_undo, _redo, after: false, tell: false))
        {
            any = true;
        }
        if (any)
        {
            Edited();
        }
    }

    /// <summary>
    /// Redoes up to and including this one; nothing when it is no longer on the redo list (redone or dropped since it
    /// was picked).
    /// </summary>
    public void ForwardTo(Change c)
    {
        if (!Holds(_redo, c))
        {
            return;
        }
        bool any = false;
        while (!ReferenceEquals(Last(_undo), c) && Step(_redo, _undo, after: true, tell: false))
        {
            any = true;
        }
        if (any)
        {
            Edited();
        }
    }

    /// <summary>
    /// Undoes the last step.
    /// </summary>
    /// <returns>False during a stroke or when there is nothing to undo.</returns>
    public bool Undo() => Step(_undo, _redo, after: false);

    /// <summary>
    /// Redoes the last undone step.
    /// </summary>
    /// <returns>False during a stroke or when there is nothing to redo.</returns>
    public bool Redo() => Step(_redo, _undo, after: true);
}

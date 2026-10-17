using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class HistoryPanel
{
    /// <summary>Every change of Claude's taken out (newest first), the user's kept.</summary>
    internal async Task TakeBackClaudes()
    {
        if (_session() is not { } s || ClaudesChanges(s) is not { Count: > 0 } steps || !await Allow(steps))
        {
            return;
        }
        // RemoveChange does nothing during a brush stroke (the card can be opened with L while the brush is down).
        if (s.Stroking)
        {
            Message?.Invoke(Said.Of("history.msg.finishStroke"));
            return;
        }
        foreach (var c in steps)
        {
            s.RemoveChange(c);
        }
        // A step refused (RemoveChange: a later step of yours moved or deleted what it added) is not marked Removed.
        int kept = steps.Count(c => !c.Removed), taken = steps.Count - kept;
        // Each removal is a step of its own: one Ctrl+Z brings one back, not all.
        Message?.Invoke(Said.Of("history.msg.tookBack", taken)
            + (taken > 1 ? Said.Of("history.msg.oneAtATime") : Said.Empty)
            + (kept > 0 ? Said.Of("history.msg.notTakenBack", kept) : Said.Empty));
        Refresh();
    }

    /// <summary>Shows or hides the card.</summary>
    public void Toggle()
    {
        Card.IsVisible = !Card.IsVisible;
        Refresh();
    }
}

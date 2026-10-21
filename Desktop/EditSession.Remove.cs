using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Taking a change out of the history while keeping what was done after it.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Takes out one change and keeps everything done after it: its height change is taken off the points it touched
    /// (later edits there stay), paint goes back where nothing repainted it since, its objects and zone resets go back.
    /// The removal is itself a change that can be undone. A change whose added objects a later step moved or deleted
    /// is refused: taking it out would bring back what it replaced beside the later step's copies (undo instead).
    /// </summary>
    /// <returns>A message for the status line (empty during a stroke, when nothing was done).</returns>
    public Said RemoveChange(Change c)
    {
        if (c.Removed || c.RevertOf != null)
        {
            return Said.Of("history.msg.cannotRemove");
        }
        Change removal;
        lock (_lock)
        {
            if (_stroke != null)
            {
                return Said.Empty;
            }
            // Undone or dropped since it was picked: its delta would go onto ground that no longer has it.
            if (!Holds(_undo, c))
            {
                return Said.Of("history.msg.notInHistory");
            }
            if (c.Things.Any(t => t.Before && !t.After && Scene.Things[t.Index].Gone && TakenLater(c, t.Index)))
            {
                return Said.Of("history.msg.takenLater");
            }
            var start = Ground.Snapshot();
            RevertPoints(c);
            int[] pts = c.Points;
            var things = c.Things.Select(t => (t.Index, Scene.Things[t.Index].Gone, t.Before)).Where(t => t.Gone != t.Before).ToArray();
            var hidden = c.Hidden.Select(t => (t.Id, Edits.IsGone(t.Id), t.Before)).Where(t => t.Item2 != t.Before).ToArray();
            var resets = ResetsToRevert(c);
            removal = new Change(Lang.T("history.undo.removed", c.Label), pts, Pick(start, pts), Pick(Ground.Snapshot(), pts), c.Zones) { Things = things, Hidden = hidden, Resets = resets, RevertOf = c };
            if (pts.Length > 0)
            {
                Touch(BoxOf(pts));
            }
            Send(c.Zones);
            SetGone(things.Select(t => (t.Index, t.Before)));
            SetHiddenGone(hidden.Select(t => (t.Id, t.Before)));
            SetResets(resets, after: true);
            c.Removed = true;
        }
        AddChange(removal);
        if (removal.Things.Length > 0)
        {
            Notify(removal.Things.Select(t => t.Index));
        }
        Edited();
        return Said.Of("history.msg.removed", c.Label);
    }
}

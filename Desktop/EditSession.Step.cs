
namespace TerrainEditor.Desktop;

/// <summary>
/// One undo or redo step: the ground, things and zone resets put as the step had them.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Undo or redo: moves the last change of one list to the other and puts its ground points, things and zone resets
    /// as they were before it (after: false) or after it (after: true).
    /// </summary>
    /// <param name="from">The list the change is taken from.</param>
    /// <param name="to">The list it goes to.</param>
    /// <param name="after">Puts things as they were after it (redo), else before (undo).</param>
    /// <param name="tell">Raises Changed and EditMade (false: the caller does, once for several steps).</param>
    /// <returns>False during a stroke or when the list is empty.</returns>
    private bool Step(List<Change> from, List<Change> to, bool after, bool tell = true)
    {
        Interlocked.Increment(ref _generation);
        Change c;
        lock (_lock)
        {
            if (_stroke != null || from.Count == 0)
            {
                return false;
            }
            c = from[^1];
            from.RemoveAt(from.Count - 1);
            var v = after ? c.After : c.Before;
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = 0, z1 = 0;
            for (int k = 0; k < c.Points.Length; k++)
            {
                int p = c.Points[k];
                Ground.Level[p] = v.Level[k];
                Ground.Smooth[p] = v.Smooth[k];
                Ground.Mod[p] = v.Mod[k];
                Ground.Lift[p] = v.Lift[k];
                Array.Copy(v.Paint, k * 4, Ground.Paint, p * 4, 4);
                Ground.PMod[p] = v.PMod[k];
                int gx = p % Ground.W, gz = p / Ground.W;
                x0 = Math.Min(x0, gx); z0 = Math.Min(z0, gz); x1 = Math.Max(x1, gx); z1 = Math.Max(z1, gz);
            }
            to.Add(c);
            if (c.Points.Length > 0)
            {
                Touch((x0 - 1, z0 - 1, x1 + 1, z1 + 1));
            }
            Send(c.Zones);
            SetGone(c.Things.Select(t => (t.Index, after ? t.After : t.Before)));
            SetResets(c.Resets, after);
            c.RevertOf?.Removed = after;
        }
        if (c.Things.Length > 0)
        {
            Notify(c.Things.Select(t => t.Index));
        }
        if (tell)
        {
            Edited();
        }
        return true;
    }

    /// <summary>
    /// Whether a step after <paramref name="c"/> that is still in (not taken out) took the thing away: moved or deleted
    /// it. A thing gone without a step (removed in the game, TakeGameChanges) does not count. Under the lock.
    /// </summary>
    /// <param name="c">The change about to be taken out.</param>
    /// <param name="index">A thing it added, an index into the scene's things.</param>
    private bool TakenLater(Change c, int index)
    {
        int at = _undo.IndexOf(c);
        return at >= 0 && _undo.Skip(at + 1).Any(l => !l.Removed && l.Things.Any(t => t.Index == index && t.After));
    }
}

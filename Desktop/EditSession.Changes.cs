
namespace TerrainEditor.Desktop;

/// <summary>
/// Putting changes on the history, and the small helpers the steps share (points, boxes, the edit store's zones).
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Puts a change on the undo list (dropping the oldest past HistoryLength) and clears the redo list.
    /// </summary>
    private void AddChange(Change c)
    {
        string label = Labelled(c.Label);
        if (label != c.Label)
        {
            c = c with { Label = label };
        }
        Interlocked.Increment(ref _generation);
        lock (_lock)
        {
            _undo.Add(c);
            if (_undo.Count > HistoryLength)
            {
                _undo.RemoveAt(0);
            }
            _redo.Clear();
            _shelvedRedo.Clear();
        }
    }

    /// <summary>
    /// The label of a new step: "Claude: " in front while Claude is setting a control (ClaudeTools.Acting) and the
    /// label lacks it, so a step made by a control Claude pressed is named like every other step of Claude's.
    /// </summary>
    private static string Labelled(string label) =>
        ClaudeTools.Acting.Value && !label.StartsWith("Claude:", StringComparison.Ordinal) ? "Claude: " + label : label;

    /// <summary>
    /// The values of these points only, in their order.
    /// </summary>
    private static Ground.State Pick(Ground.State all, int[] pts) => new(
        [.. pts.Select(p => all.Level[p])], [.. pts.Select(p => all.Smooth[p])], [.. pts.Select(p => all.Mod[p])],
        [.. pts.SelectMany(p => new[] { all.Paint[p * 4], all.Paint[p * 4 + 1], all.Paint[p * 4 + 2], all.Paint[p * 4 + 3] })], [.. pts.Select(p => all.PMod[p])],
        [.. pts.Select(p => all.Lift[p])]);

    /// <summary>
    /// Puts a ground change on the undo list (its points' values from start to now) and clears the redo list. Called
    /// inside the lock.
    /// </summary>
    private void Record(string label, Ground.State start, IEnumerable<int> touched, List<(int X, int Z)> zones)
    {
        int[] pts = [.. touched.Distinct().Order()];
        AddChange(new Change(label, pts, Pick(start, pts), Pick(Ground.Snapshot(), pts), zones));
    }

    /// <summary>
    /// The zones to the edit store, as the web page uploads them.
    /// </summary>
    private void Send(List<(int X, int Z)> zones)
    {
        foreach (var (zx, zz) in zones)
        {
            Edits.Put(Ground.ZoneEdit(zx, zz));
        }
    }

    /// <summary>
    /// The grid box of some points (all the ground's grid when there are none), a point wider on every side: their
    /// neighbours' shading changes with them.
    /// </summary>
    private (int X0, int Z0, int X1, int Z1) BoxOf(IReadOnlyCollection<int> points)
    {
        if (points.Count == 0)
        {
            return (0, 0, Ground.W - 1, Ground.H - 1);
        }
        int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
        foreach (int p in points)
        {
            int gx = p % Ground.W, gz = p / Ground.W;
            (x0, z0, x1, z1) = (Math.Min(x0, gx), Math.Min(z0, gz), Math.Max(x1, gx), Math.Max(z1, gz));
        }
        return (x0 - 1, z0 - 1, x1 + 1, z1 + 1);
    }

    /// <summary>
    /// The scene's heights, paint mask and limit marks follow the ground over a grid rectangle (clamped to the grid),
    /// which joins the dirty rectangle.
    /// </summary>
    private void Touch((int X0, int Z0, int X1, int Z1) r)
    {
        int x0 = Math.Max(0, r.X0), z0 = Math.Max(0, r.Z0), x1 = Math.Min(Ground.W - 1, r.X1), z1 = Math.Min(Ground.H - 1, r.Z1);
        Scene.Refresh(Ground, x0, z0, x1, z1);
        _dirty = _dirty is { } d ? (Math.Min(d.X0, x0), Math.Min(d.Z0, z0), Math.Max(d.X1, x1), Math.Max(d.Z1, z1)) : (x0, z0, x1, z1);
    }

    /// <summary>
    /// The grid rectangle changed since the last call (null: nothing), for the view to send to the graphics card.
    /// </summary>
    public (int X0, int Z0, int X1, int Z1)? TakeDirty()
    {
        lock (_lock)
        {
            var d = _dirty;
            _dirty = null;
            return d;
        }
    }
}

using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// The history fitted to the game's save: objects by their ids there (or kept as gone objects an undo or redo brings
    /// back), every step from before the game's save (asked about before it is undone). A step touching what the game's
    /// version was kept for cannot be undone, nor the steps before it: dropped, and with them a redo step touching it
    /// and those after it.
    /// </summary>
    /// <returns>The history (null: none left), how many steps it has, how many were dropped.</returns>
    private (EditSession.Kept? History, int Steps, int Dropped) Refit(EditSession.Kept? k, EditReplay plan, EditReplay.Replayed r)
    {
        if (k == null)
        {
            return (null, 0, 0);
        }
        bool Touches(EditSession.Change c)
        {
            var f = k.FrameOf(c);
            return k.Ids[c].Any(r.Dropped.Contains) || c.Zones.Any(z => r.GameGround.Contains((z.X + f.X0, z.Z + f.Z0)))
                || c.Resets.Any(x => x.Reset.Ground && r.GameGround.Contains((x.Reset.X, x.Reset.Z)));
        }
        int undoFrom = k.Undo.FindLastIndex(Touches) + 1;
        int redoFrom = k.Redo.FindLastIndex(Touches) + 1;
        var undo = k.Undo.Skip(undoFrom).ToList();
        var redo = k.Redo.Skip(redoFrom).ToList();
        int dropped = undoFrom + redoFrom;
        if (undo.Count + redo.Count == 0)
        {
            return (null, 0, dropped);
        }
        var gone = new List<NewObject>();
        var goneIds = new Dictionary<int, int>();
        int Fitted(int id)
        {
            if (plan.NewerId(id) is int y)
            {
                return y;
            }
            if (id < 0)
            {
                return id;
            }
            if (!goneIds.TryGetValue(id, out int g))
            {
                var o = plan.Gone(id, NextId());
                gone.Add(o);
                goneIds[id] = g = o.Id;
            }
            return g;
        }
        // Oldest first (the undo list, then the redo list from its last), so a removal finds the step it took out.
        var map = new Dictionary<EditSession.Change, EditSession.Change>();
        var ids = new Dictionary<EditSession.Change, int[]>();
        var frames = new Dictionary<EditSession.Change, EditSession.Frame>();
        foreach (var c in undo.Concat(Enumerable.Reverse(redo)))
        {
            var f = c with { Earlier = true, RevertOf = c.RevertOf != null && map.TryGetValue(c.RevertOf, out var was) ? was : null };
            map[c] = f;
            ids[f] = [.. k.Ids[c].Select(Fitted)];
            frames[f] = k.FrameOf(c);
        }
        Edits.KeepGone(gone);
        var fitted = new EditSession.Kept([.. undo.Select(c => map[c])], [.. redo.Select(c => map[c])], k.Ox, k.Oz, k.W, k.X0, k.Z0, ids, frames);
        return (fitted, undo.Count + redo.Count, dropped);
    }
}

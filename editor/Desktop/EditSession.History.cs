
namespace TerrainEditor.Desktop;

/// <summary>
/// The history as it follows the open world from area to area: exporting it, and the frames its points are in.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// A history kept with the open world while another area is open, to fit into the next area opened (Import), and
    /// what HistoryFile writes.
    /// </summary>
    /// <param name="Undo">The undo list, oldest first.</param>
    /// <param name="Redo">The redo list, the next redo last.</param>
    /// <param name="Ox">The world x in metres of the grid's first point in the area it came from.</param>
    /// <param name="Oz">Its world z in metres.</param>
    /// <param name="W">That area's grid width (ground points are its indices).</param>
    /// <param name="X0">That area's first zone x.</param>
    /// <param name="Z0">That area's first zone z.</param>
    /// <param name="Ids">For each change, the ids of its things, in the order of its Things.</param>
    /// <param name="Frames">
    /// The frame of each change made in another area than the kept one (null, or a change not in it: the kept frame).
    /// </param>
    public sealed record Kept(List<Change> Undo, List<Change> Redo, int Ox, int Oz, int W, int X0, int Z0, Dictionary<Change, int[]> Ids, Dictionary<Change, Frame>? Frames = null)
    {
        /// <summary>The frame a change's points and zones are in: its own when it has one, else the kept one.</summary>
        public Frame FrameOf(Change c) => Frames != null && Frames.TryGetValue(c, out var f) ? f : new Frame(Ox, Oz, W, X0, Z0);

        /// <summary>
        /// The history as the world holds it once the changes not saved or applied are dropped (the world read again:
        /// live Reload or Discard), as <see cref="UndoUnapplied"/> leaves an area's: the steps not applied at its end
        /// go, undone steps the world still has (applied, then undone) are done again, and nothing is left to redo.
        /// </summary>
        public Kept AsApplied()
        {
            var undo = new List<Change>(Undo);
            var redo = new List<Change>(Redo);
            while (undo.Count > 0 && !undo[^1].Applied)
            {
                undo.RemoveAt(undo.Count - 1);
            }
            while (redo.Count > 0 && redo[^1].Applied)
            {
                undo.Add(redo[^1]);
                redo.RemoveAt(redo.Count - 1);
            }
            return this with { Undo = undo, Redo = [] };
        }
    }

    /// <summary>The area a kept change's points and zones are indices in.</summary>
    /// <param name="Ox">The world x in metres of the grid's first point.</param>
    /// <param name="Oz">Its world z in metres.</param>
    /// <param name="W">The grid's width (ground points are its indices).</param>
    /// <param name="X0">The area's first zone x.</param>
    /// <param name="Z0">The area's first zone z.</param>
    public readonly record struct Frame(int Ox, int Oz, int W, int X0, int Z0);

    /// <summary>
    /// The history as Kept: this area's steps (ground points by world position and objects by id) and those made in
    /// other areas together, in the order they were made, each in the frame it came in, to follow the world to the
    /// next area.
    /// </summary>
    public Kept Export()
    {
        lock (_lock)
        {
            var frame = new Frame(Scene.X0 * 64 - 32, Scene.Z0 * 64 - 32, Ground.W, Scene.X0, Scene.Z0);
            var ids = new Dictionary<Change, int[]>();
            var frames = new Dictionary<Change, Frame>();
            foreach (var (c, f, cids) in _shelvedUndo.Concat(_shelvedRedo))
            {
                ids[c] = cids;
                frames[c] = f;
            }
            foreach (var c in _undo.Concat(_redo))
            {
                ids[c] = [.. c.Things.Select(t => Scene.Things[t.Index].Id)];
                frames[c] = frame;
            }
            // Steps made here since the last import come after every other, the undo list's in its order, then the
            // redo list's from its last (the next redo, the oldest of them) to its first.
            foreach (var c in _undo)
            {
                OrderOf(c);
            }
            for (int i = _redo.Count - 1; i >= 0; i--)
            {
                OrderOf(_redo[i]);
            }
            foreach (var c in _order.Keys.Where(c => !ids.ContainsKey(c)).ToList())
            {
                _order.Remove(c);
            }
            var undo = _shelvedUndo.Select(s => s.Change).Concat(_undo).OrderBy(OrderOf).ToList();
            var redo = _shelvedRedo.Select(s => s.Change).Concat(_redo).OrderByDescending(OrderOf).ToList();
            return new Kept(undo, redo, frame.Ox, frame.Oz, frame.W, frame.X0, frame.Z0, ids, frames);
        }
    }

    /// <summary>Where a kept change stands to this area.</summary>
    internal enum Standing
    {
        /// <summary>Its ground and objects are all in the area: it can be undone here.</summary>
        Fits,
        /// <summary>None of them is in the area (made elsewhere): kept aside, it comes back where it was made.</summary>
        Outside,
        /// <summary>Some are in the area, some not: it cannot be undone here, nor can what came before it.</summary>
        Straddles,
    }
}

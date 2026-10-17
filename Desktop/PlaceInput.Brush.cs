using Avalonia;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    // ---- Brush strokes.
    /// <summary>A brush stroke, from press to release: one undo step.</summary>
    private sealed class Stroke
    {
        /// <summary>Shift + drag: removes the chosen kinds instead of placing.</summary>
        public bool Erase;
        /// <summary>Where the press was, in pixels; past 6 px it is a drag.</summary>
        public Point From;
        /// <summary>The mouse moved far enough to paint (or erase).</summary>
        public bool Dragging;
        /// <summary>What the preview showed at the press: what a click places.</summary>
        public List<PlaceTool.Placement> Stamp = [];
        /// <summary>What the drag has painted so far (the stamp included).</summary>
        public List<PlaceTool.Placement> Added = [];
        /// <summary>Erasing: the things to remove (indices into the scene's things).</summary>
        public HashSet<int> Removed = [];
        /// <summary>When the stroke last painted (ms on the input's clock).</summary>
        public long Last;
    }

    /// <summary>The stroke under way, or null.</summary>
    private Stroke? _stroke;
    /// <summary>Paints about 30 times a second during a stroke.</summary>
    private readonly DispatcherTimer _timer;
    /// <summary>Times the paint steps.</summary>
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// A paint step of the stroke under way: erases under the brush, or adds placements around the cursor (not while
    /// building).
    /// </summary>
    private void Paint()
    {
        if (_stroke is not { Dragging: true } s || _at is not { } at)
        {
            return;
        }
        long now = _clock.ElapsedMilliseconds;
        float dt = (now - s.Last) / 1000f;
        s.Last = now;
        if (s.Erase)
        {
            foreach (int i in Tool.EraseAt(at))
            {
                s.Removed.Add(i);
            }
            _view.Select(s.Removed);
            return;
        }
        // Building (the Workshop) or One at a time: a click puts one; a drag does not paint more.
        if (Tool.Building || Tool.OneAtATime)
        {
            return;
        }
        s.Added.AddRange(Tool.PaintStep(at, dt, s.Added));
        Shown = [.. s.Added];
        _view.LassoChanged();
    }

    /// <summary>
    /// Commits a finished stroke as one undo step (the removed things, or the click's or drag's placements) and says
    /// what it did.
    /// </summary>
    private void EndStroke(Stroke s)
    {
        _view.Select([]);
        if (Session() is not { } session)
        {
            return;
        }
        if (s.Erase)
        {
            EndErase(session, s);
        }
        else
        {
            EndPlace(session, s);
        }
        Tool.NewLayout();
        Refresh();
    }

    /// <summary>Commits the things a brush stroke removed, as one undo step, and says so.</summary>
    /// <param name="session">The edit session the removal is committed to.</param>
    /// <param name="s">The finished stroke.</param>
    private void EndErase(EditSession session, Stroke s)
    {
        if (s.Removed.Count > 0)
        {
            session.Commit(Lang.T("place.undo.removed", s.Removed.Count), null, s.Removed, []);
        }
        Message?.Invoke(Said.Of("place.msg.removed", s.Removed.Count));
    }
}

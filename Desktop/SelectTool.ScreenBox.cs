using Avalonia;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>The rectangle drawn on the screen as the pointer moves: shown once it has moved past 6 px.</summary>
    /// <param name="b">The rectangle as it stands.</param>
    /// <param name="at">The point in the view.</param>
    private void DrawBox((Point From, Point To, bool Add, bool Drawing) b, Point at)
    {
        _box = b with { To = at, Drawing = b.Drawing || Point.Distance(at, b.From) > 6 };
        ScreenBox = _box.Value.Drawing ? Spanning(b.From, at) : null;
        _view.LassoChanged();
    }

    /// <summary>
    /// The rectangle let go: selects the objects seen inside it (GlView.SeenIn: not those behind others), among the
    /// shown ones; a click without a drag deselects, unless it adds (Shift).
    /// </summary>
    /// <param name="b">The rectangle as it stands.</param>
    /// <param name="size">The view's size.</param>
    private void EndBox((Point From, Point To, bool Add, bool Drawing) b, Size size)
    {
        StopZones();
        if (!b.Drawing)
        {
            ClickedEmpty(b.Add);
            return;
        }
        var shown = Shown().ToHashSet();
        ZoneSelected([.. _view.SeenIn(Spanning(b.From, b.To), size).Where(shown.Contains)], b.Add);
    }

    /// <summary>The rectangle between two corners, whichever way it was dragged (left or up too).</summary>
    /// <param name="a">One corner.</param>
    /// <param name="b">The opposite corner.</param>
    internal static Rect Spanning(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>A click on empty ground (a zone or a rectangle not drawn): deselects, unless it adds (Shift).</summary>
    /// <param name="add">Shift held.</param>
    private void ClickedEmpty(bool add)
    {
        if (!add)
        {
            _view.Select([]);
        }
    }

    /// <summary>Selects what a zone or a rectangle took (add: to the selection), and says how many.</summary>
    /// <param name="ids">The objects it took.</param>
    /// <param name="add">Shift held.</param>
    private void ZoneSelected(List<int> ids, bool add)
    {
        _view.Select(ids, add);
        Message?.Invoke(ids.Count == 0 ? Said.Of("select.msg.zoneEmpty")
            : add ? Said.Of("select.msg.zoneAdded", ids.Count, _view.Selected.Count)
            : Said.Of("select.msg.zone", ids.Count), false);
    }

    /// <summary>Drops the zone or the rectangle being drawn (let go, or Esc), and its drawing.</summary>
    private void StopZones()
    {
        _lasso = null;
        Lasso = null;
        _box = null;
        ScreenBox = null;
        _view.LassoChanged();
    }
}

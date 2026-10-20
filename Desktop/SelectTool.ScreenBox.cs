using Avalonia;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>The rectangle drawn on the screen as the pointer moves: shown once it has moved past 6 px.</summary>
    /// <param name="b">The rectangle as it stands.</param>
    /// <param name="at">The point in the view.</param>
    private void DrawBox((Point From, Point To, ZoneHow How, bool Drawing) b, Point at)
    {
        _box = b with { To = at, Drawing = b.Drawing || Point.Distance(at, b.From) > 6 };
        ScreenBox = _box.Value.Drawing ? Spanning(b.From, at) : null;
        _view.LassoChanged();
    }

    /// <summary>
    /// The rectangle let go: selects the objects seen inside it (GlView.SeenIn: not those behind others), or all those
    /// inside it with <see cref="SelectThrough"/>, among the shown ones, as the keys held say (ZoneSelected); a click
    /// without a drag deselects, unless a key is held.
    /// </summary>
    /// <param name="b">The rectangle as it stands.</param>
    /// <param name="size">The view's size.</param>
    private void EndBox((Point From, Point To, ZoneHow How, bool Drawing) b, Size size)
    {
        StopZones();
        if (!b.Drawing)
        {
            ClickedEmpty(b.How);
            return;
        }
        var shown = Shown().ToHashSet();
        var r = Spanning(b.From, b.To);
        ZoneSelected([.. (SelectThrough ? _view.InsideRect(r, size) : _view.SeenIn(r, size)).Where(shown.Contains)], b.How);
    }

    /// <summary>The rectangle between two corners, whichever way it was dragged (left or up too).</summary>
    /// <param name="a">One corner.</param>
    /// <param name="b">The opposite corner.</param>
    internal static Rect Spanning(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>A click on empty ground (a zone or a rectangle not drawn): deselects, unless a key is held.</summary>
    /// <param name="how">What the keys held make it do.</param>
    private void ClickedEmpty(ZoneHow how)
    {
        if (how == ZoneHow.Replace)
        {
            _view.Select([]);
        }
    }

    /// <summary>
    /// What a zone or a rectangle took becomes the selection, is added to it (Ctrl) or taken out of it (Shift); says
    /// how many.
    /// </summary>
    /// <param name="ids">The objects it took.</param>
    /// <param name="how">What the keys held make it do.</param>
    private void ZoneSelected(List<int> ids, ZoneHow how)
    {
        if (how == ZoneHow.Remove)
        {
            _view.Unselect(ids);
        }
        else
        {
            _view.Select(ids, how == ZoneHow.Add);
        }
        Message?.Invoke(ids.Count == 0 ? Said.Of("select.msg.zoneEmpty") : how switch
        {
            ZoneHow.Add => Said.Of("select.msg.zoneAdded", ids.Count, _view.Selected.Count),
            ZoneHow.Remove => Said.Of("select.msg.zoneRemoved", ids.Count, _view.Selected.Count),
            _ => Said.Of("select.msg.zone", ids.Count),
        }, false);
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

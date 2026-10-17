using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    /// <summary>
    /// A press on a shape or the grid: a double-click places it, a point is removed or dragged, a segment is inserted into,
    /// or a grid box starts.
    /// </summary>
    /// <param name="at">The mouse on the view, in pixels.</param>
    /// <param name="h">The grid point under the mouse.</param>
    /// <param name="ctrl">Ctrl held: remove the point under the mouse.</param>
    /// <param name="clicks">The click count.</param>
    private void DownShape(Point at, Vector2 h, bool ctrl, int clicks)
    {
        // A double-click on the last point places the shape (before the point is taken for a drag: the
        // second click of a double-click is always on it).
        if (Pointed && clicks >= 2 && Tool.Points.Count > 0 && Vector2.Distance(Tool.Points[^1], Tool.Local(h)) < 1.5f)
        {
            _press = null;
            PlaceShape();
            return;
        }
        if (Editable && Tool.Points.Count > 0)
        {
            int pt = PointAt(at);
            if (pt >= 0)
            {
                _press = null;
                if (ctrl)
                {
                    lock (Tool.Points) { Tool.Points.RemoveAt(pt); }
                    Tool.Notify();
                    return;
                }
                _dragPt = pt;
                return;
            }
            int seg = Tool.Points.Count >= (Tool.Mode == PlaceTool.Modes.Zone ? 3 : 2) ? SegmentAt(at) : -1;
            if (seg >= 0)
            {
                _press = null;
                lock (Tool.Points) { Tool.Points.Insert(seg + 1, Tool.Local(h)); }
                _dragPt = seg + 1;
                Tool.Notify();
                return;
            }
        }
        if (Tool.Mode == PlaceTool.Modes.Grid)
        {
            Tool.GridA = h;
            Tool.GridB = null;
            Tool.ClearTurn();
        }
    }
}

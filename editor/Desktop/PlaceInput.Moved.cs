using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    /// <summary>
    /// The mouse moved: follows the cursor, starts a drag past 6 px, drags points, figures and grid boxes, or draws
    /// freehand (a point every 2 m).
    /// </summary>
    public void Moved(Point at, Size size)
    {
        _pointer = at;
        _size = size;
        var hit = _view.GridAt(at, size);
        // Building: the build ray itself (the piece goes where it hits, a piece or the ground).
        if (Tool.Building)
        {
            Tool.AimRay = _view.WorldRay(at, size);
            hit ??= Tool.AimRay != null ? Vector2.Zero : null;
        }
        _at = hit;
        if (_stroke is { } s)
        {
            if (!s.Dragging && Point.Distance(at, s.From) > 6)
            {
                s.Dragging = true;
                // Painting: the ground under the click gets the preview too, then the brush adds more. Building keeps
                // its one piece where it is: the click puts it, however far the mouse went (EndStroke).
                if (!s.Erase && !Tool.Building)
                {
                    s.Added.AddRange(s.Stamp);
                    s.Stamp.Clear();
                }
            }
            return;
        }
        if (hit is not { } h)
        {
            Refresh();
            return;
        }
        if (_press is { } && Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape != PlaceTool.LineShapes.Points)
        {
            Tool.FigB = h;
            Tool.Notify();
            return;
        }
        if (_dragPt is int dp)
        {
            lock (Tool.Points) { Tool.Points[dp] = Tool.Local(h); }
            Tool.Notify();
            return;
        }
        if (DragPress(at, h))
        {
            return;
        }
        Refresh();
    }
}

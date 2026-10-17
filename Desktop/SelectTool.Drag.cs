using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>The pointer moved with the left button down: drags the handle, draws the zone, or moves the selection.</summary>
    /// <param name="at">The point in the view.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="ctrl">Ctrl held: handle drags go in 15° or half-metre steps.</param>
    public void Moved(Point at, Size size, bool ctrl = false)
    {
        if (_handleDrag is { } hd)
        {
            DragHandle(hd, at, size, ctrl);
            return;
        }
        if (_lasso is { } l)
        {
            DrawLasso(l, at, size);
            return;
        }
        if (_drag is not { } d)
        {
            return;
        }
        if (!_dragMoving && Point.Distance(at, d.At) > 5)
        {
            _dragMoving = true;
            _commitTimer.Stop();
            Begin();
        }
        if (_dragMoving && _move is { } m && _view.WorldAt(at, size) is { } g2)
        {
            m.Dx = g2.X - d.From.X;
            m.Dz = g2.Z - d.From.Z;
            Preview(m);
        }
    }

    /// <summary>
    /// A handle drag as the pointer moves: the turn around the ring, or the slide along the handle's axis; the preview
    /// follows when the pointer has a place to move to.
    /// </summary>
    /// <param name="hd">The handle drag as it began.</param>
    /// <param name="at">The point in the view.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="ctrl">Ctrl held.</param>
    private void DragHandle((Gizmo.Handle Handle, Vector3 Centre, float Start, float Dx, float Dz, float Dy, float Turn) hd, Point at, Size size, bool ctrl)
    {
        if (_move is not { } mv)
        {
            return;
        }
        var (ro, rd) = _view.RayAt(at, size);
        bool moved = hd.Handle == Gizmo.Handle.Ring ? TurnBy(mv, hd, ro, rd, ctrl) : SlideBy(mv, hd, ro, rd, ctrl);
        if (moved)
        {
            Preview(mv);
        }
    }
}

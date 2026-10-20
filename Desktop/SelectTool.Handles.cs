using Avalonia;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>The handle under a point of the view, if the gizmo shows.</summary>
    private Gizmo.Handle? HandleAt(Point at, Size size)
    {
        if (_view.GizmoAt() is not var (c, scale))
        {
            return null;
        }
        var (o, d) = _view.RayAt(at, size);
        return Gizmo.Hit(o, d, c, scale);
    }

    /// <summary>The pointer moved with no button down: highlights the handle under it.</summary>
    public void Hover(Point at, Size size)
    {
        var h = HandleAt(at, size);
        if (h != _hover)
        {
            _hover = h;
            _view.LassoChanged();
        }
    }

    /// <summary>
    /// A press on a handle starts dragging it along its axis, or around the ring. Not with Shift held (taking objects
    /// out, or a Ctrl + Shift + click's row): those objects may lie under the handles.
    /// </summary>
    /// <param name="at">The point in the view.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="keys">The keys held.</param>
    /// <returns>True when the press was on a handle, which then takes it.</returns>
    private bool PressHandle(Point at, Size size, KeyModifiers keys)
    {
        if (!keys.HasFlag(KeyModifiers.Shift) && HandleAt(at, size) is { } handle && _view.GizmoAt() is var (c, _))
        {
            _commitTimer.Stop();
            if (Begin() is not { } mv)
            {
                return true;
            }
            var (o, d) = _view.RayAt(at, size);
            float? start = handle == Gizmo.Handle.Ring ? Gizmo.Heading(o, d, c) : Gizmo.Along(o, d, c, Gizmo.Axis(handle));
            if (start is float st)
            {
                _handleDrag = (handle, c, st, mv.Dx, mv.Dz, mv.Dy, mv.Turn);
            }
            return true;
        }
        return false;
    }
}

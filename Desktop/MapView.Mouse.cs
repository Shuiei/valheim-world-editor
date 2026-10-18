using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL.Controls;
using Vector2 = System.Numerics.Vector2;

namespace TerrainEditor.Desktop;

/// <summary>
/// The mouse over the map: a drag slides the view, the wheel zooms toward the mouse, and a click picks a spot.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    // ---- The mouse.
    /// <summary>
    /// Where the mouse was pressed, and where it was last while dragging; null when not pressed.
    /// </summary>
    private Point? _press, _last;

    /// <summary>
    /// The world point (x east, z north) under a point of the control.
    /// </summary>
    public Vector2 WorldAt(Point p) => new(Center.X + (float)(p.X - Bounds.Width / 2) * MetersPerPixel, Center.Y - (float)(p.Y - Bounds.Height / 2) * MetersPerPixel);

    /// <summary>
    /// The OpenGL picture cannot be hit by the pointer (like the 3D view): a transparent surface laid over it takes the
    /// mouse and hands it here.
    /// </summary>
    public void Attach(Control surface)
    {
        surface.PointerPressed += (_, e) =>
        {
            _press = _last = e.GetPosition(surface);
            e.Pointer.Capture(surface);
        };
        surface.PointerMoved += (_, e) =>
        {
            var p = e.GetPosition(surface);
            var w = WorldAt(p);
            Hovered?.Invoke(w.X, w.Y);
            if (_last is { } l)
            {
                Center += new Vector2(-(float)(p.X - l.X) * MetersPerPixel, (float)(p.Y - l.Y) * MetersPerPixel);
                _last = p;
                RequestNextFrameRendering();
                ViewChanged?.Invoke();
            }
        };
        surface.PointerReleased += (_, e) =>
        {
            var p = e.GetPosition(surface);
            if (_press is { } from && Point.Distance(from, p) < 5 && e.InitialPressMouseButton == MouseButton.Left)
            {
                var w = WorldAt(p);
                Picked?.Invoke(w.X, w.Y);
            }
            _press = _last = null;
            e.Pointer.Capture(null);
            RequestNextFrameRendering();
        };
        surface.PointerWheelChanged += (_, e) =>
        {
            // Toward the mouse: the point under it stays there.
            var p = e.GetPosition(surface);
            var before = WorldAt(p);
            MetersPerPixel = Math.Clamp(MetersPerPixel * MathF.Pow(0.8f, (float)e.Delta.Y), 0.25f, 40f);
            Center += before - WorldAt(p);
            RequestNextFrameRendering();
            ViewChanged?.Invoke();
            e.Handled = true;
        };
    }
}

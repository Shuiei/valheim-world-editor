using Avalonia.Controls;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The pointer: the wheel zooms, a drag moves the view, and the pointer's world point is told.
public sealed partial class SeedMap : Control, IDisposable
{
    /// <summary>The wheel zooms where the pointer is.</summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoom(Math.Pow(1.25, -e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>A double click shows all of the world; a left press starts a drag.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.ClickCount == 2)
        {
            Fit();
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _drag = e.GetPosition(this);
            e.Pointer.Capture(this);
        }
    }

    /// <summary>Drags the view, and tells where the pointer is.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_drag is { } from)
        {
            CenterX -= (float)(p.X - from.X) * MetersPerPixel;
            CenterZ += (float)(p.Y - from.Y) * MetersPerPixel;
            _drag = p;
            _fit = false;
            Changed();
        }
        var (x, z) = WorldAt(p);
        Hovered?.Invoke(x, z);
    }

    /// <summary>Ends a drag.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _drag = null;
        e.Pointer.Capture(null);
    }
}

using Avalonia.Controls;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The pointer: the wheel zooms, a drag moves the view, and the pointer's world point is told; over the map itself
// and over a layer laid on it (its marks).
public sealed partial class SeedMap : Control, IDisposable
{
    /// <summary>
    /// The pointer over a layer laid on the map (its marks) is the map's: a press, a drag or the wheel there acts as on
    /// the map, so a drag that begins on a mark (the start's, in the middle once the world is fitted) moves the view.
    /// </summary>
    /// <param name="layer">The layer over the map.</param>
    public void TakePointerOver(Control layer)
    {
        layer.AddHandler(PointerWheelChangedEvent, (_, e) => Wheel(e));
        layer.AddHandler(PointerPressedEvent, (_, e) => Press(e));
        layer.AddHandler(PointerMovedEvent, (_, e) => Move(e));
        layer.AddHandler(PointerReleasedEvent, (_, e) => Release(e));
    }

    /// <summary>The wheel zooms where the pointer is.</summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Wheel(e);
    }

    /// <summary>A double click shows all of the world; a left press starts a drag.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Press(e);
    }

    /// <summary>Drags the view, and tells where the pointer is.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Move(e);
    }

    /// <summary>Ends a drag.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Release(e);
    }

    /// <summary>The wheel zooms where the pointer is.</summary>
    /// <param name="e">The wheel's turn.</param>
    private void Wheel(PointerWheelEventArgs e)
    {
        Zoom(Math.Pow(1.25, -e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>A double click shows all of the world; a left press starts a drag, the map taking the pointer.</summary>
    /// <param name="e">The press.</param>
    private void Press(PointerPressedEventArgs e)
    {
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
    /// <param name="e">The move.</param>
    private void Move(PointerEventArgs e)
    {
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
    /// <param name="e">The release.</param>
    private void Release(PointerReleasedEventArgs e)
    {
        _drag = null;
        e.Pointer.Capture(null);
    }
}

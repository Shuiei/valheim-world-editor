using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The button going down on the surface: the eyedropper, the slide (Space or Shift + drag), then the tool's own press
// (GlView.Input.Press.Tools.cs, .Click.cs, .Brush.cs).
public sealed partial class GlView
{
    /// <summary>
    /// A press on the surface: records where it went down, then the first of the eyedropper, the slide and the tool
    /// takes it. Nothing takes it: the view wakes and the press ends.
    /// </summary>
    /// <param name="surface">The surface the press is on.</param>
    /// <param name="e">The press.</param>
    private void OnPointerPressed(Control surface, PointerPressedEventArgs e)
    {
        surface.Focus(NavigationMethod.Pointer);
        var p = e.GetCurrentPoint(surface);
        _pressAt = p.Position;
        _dragFrom = p.Position;
        _dragButton = p.Properties.PointerUpdateKind;
        _pointer = _heldAt = p.Position;
        _surfaceSize = surface.Bounds.Size;
        e.Pointer.Capture(surface);
        if (PressEyedropper(p.Position))
        {
            return;
        }
        if (PressPan(e))
        {
            return;
        }
        if (_dragButton == PointerUpdateKind.LeftButtonPressed && PressLeftTool(e, p.Position))
        {
            return;
        }
        Wake();
    }

    /// <summary>
    /// The left button's press given to the tool in use: the first of the tool presses that takes it (in the order
    /// of the tools' own files), or none.
    /// </summary>
    /// <param name="e">The press.</param>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when a tool took the press.</returns>
    private bool PressLeftTool(PointerPressedEventArgs e, Point at)
    {
        return PressPlace(e, at) || PressPaste(at) || PressArea(e, at) || PressPath(e, at)
            || PressShape(at) || PressDungeon(at) || PressMeasure(at) || PressSelect(e, at)
            || PressBrushAlt(e, at) || PressStamp(at) || PressBrush(at);
    }
}

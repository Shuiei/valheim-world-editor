using Avalonia;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The left press given to the brush: Alt picks the ground's height, Raise or Lower with the stamp once puts the
// whole stamp in, and any other brush starts a stroke.
public sealed partial class GlView
{
    /// <summary>With a brush, Alt + click picks the ground's height (Alt + Shift: for the Mask). The view is not woken for it.</summary>
    /// <param name="e">The press.</param>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when a brush is in use with Alt held.</returns>
    private bool PressBrushAlt(PointerPressedEventArgs e, Point at)
    {
        if (Tool is not BrushTool || !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }
        _dragFrom = null;
        if (WorldAt(at, _surfaceSize) is { } w)
        {
            BrushAltClick?.Invoke(w.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
        return true;
    }

    /// <summary>Stamp once: a click of Raise or Lower puts the whole stamp in.</summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when that stamp is in use and took the click.</returns>
    private bool PressStamp(Point at)
    {
        if (!(Tool is BrushTool.Raise or BrushTool.Lower && Scene is { Session.Brush: { StampOnce: true, Shape: BrushShape.Stamp } } ss))
        {
            return false;
        }
        _dragFrom = null;
        if (GroundAt(ss, _lastViewProj, at, _surfaceSize) is { } g)
        {
            StampClicked?.Invoke(g.X, g.Z);
        }
        Wake();
        return true;
    }

    /// <summary>Any other brush takes the press: a stroke starts on the ground under it.</summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when a brush is in use and the stroke started.</returns>
    private bool PressBrush(Point at)
    {
        if (Tool is not BrushTool tool || Scene is not { Session: { } session } s)
        {
            return false;
        }
        _dragFrom = null;
        if (GroundAt(s, _lastViewProj, at, _surfaceSize) is { } g)
        {
            session.BeginStroke(tool, g.X, g.Z);
            _brushDown = true;
        }
        Wake();
        return true;
    }
}

using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace TerrainEditor.Desktop;

// The left press given to the tools that act on the world under the pointer: Shape and Mountain, Dungeon, Measure
// and Select.
public sealed partial class GlView
{
    /// <summary>The Shape or Mountain tool takes the press on the ground under it.</summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when one of those tools is in use and took it.</returns>
    private bool PressShape(Point at)
    {
        if (Mode is not (ToolMode.Shape or ToolMode.Mountain))
        {
            return false;
        }
        _dragFrom = null;
        if (Scene is { } sc && GroundAt(sc, _lastViewProj, at, _surfaceSize) is { } g)
        {
            ShapeClicked?.Invoke(g.X, g.Z);
        }
        Wake();
        return true;
    }

    /// <summary>The Dungeon tool takes the press: it hovers at the ray under the pointer, then clicks.</summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the Dungeon tool is in use and took it.</returns>
    private bool PressDungeon(Point at)
    {
        if (Mode != ToolMode.Dungeon)
        {
            return false;
        }
        _dragFrom = null;
        if (WorldRay(at, _surfaceSize) is { } ray)
        {
            Dungeon.Hover(ray.O, ray.D);
        }
        Dungeon.Click();
        Wake();
        return true;
    }

    /// <summary>The Measure tool takes the press: the tape starts at the world point under it.</summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the Measure tool is in use and took it.</returns>
    private bool PressMeasure(Point at)
    {
        if (Mode != ToolMode.Measure)
        {
            return false;
        }
        _dragFrom = null;
        if (WorldAt(at, _surfaceSize) is { } w)
        {
            Tape.Down(w);
        }
        Wake();
        return true;
    }

    /// <summary>
    /// The Select mode with no tool takes the press: Ctrl or Shift adds to the selection, Shift alone takes the row
    /// from the last object clicked, and a double click opens the inspector.
    /// </summary>
    /// <param name="e">The press.</param>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the select drag is started.</returns>
    private bool PressSelect(PointerPressedEventArgs e, Point at)
    {
        if (Tool != null || !SelectMode)
        {
            return false;
        }
        _dragFrom = null;
        _selectDown = true;
        // Ctrl + click (or Shift, Ctrl + zone): added to what is selected; Shift + click: the row
        // from the last object clicked.
        SelectTool.Range = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !Ctrl(e.KeyModifiers);
        SelectTool.Down(at, _surfaceSize, e.KeyModifiers.HasFlag(KeyModifiers.Shift) || Ctrl(e.KeyModifiers), e.KeyModifiers.HasFlag(KeyModifiers.Alt), e.ClickCount);
        if (e.ClickCount == 2)
        {
            // A double click on an object: its inspector (a chest's contents, a sign's text...).
            Dispatcher.UIThread.Post(() => InspectAsked?.Invoke());
        }
        Wake();
        return true;
    }
}

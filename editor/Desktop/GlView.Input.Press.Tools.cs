using Avalonia;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The left press given to the tools that draw or place: Place, Paste, Area and Path.
public sealed partial class GlView
{
    /// <summary>The Place tool takes the press: its own press handler, with the modifier keys.</summary>
    /// <param name="e">The press.</param>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the Place tool is in use and took it.</returns>
    private bool PressPlace(PointerPressedEventArgs e, Point at)
    {
        if (Mode != ToolMode.Place || Place == null)
        {
            return false;
        }
        _dragFrom = null;
        _placeDown = true;
        var m = e.KeyModifiers;
        Place.Down(at, _surfaceSize, m.HasFlag(KeyModifiers.Shift), Ctrl(m), m.HasFlag(KeyModifiers.Alt), e.ClickCount);
        Wake();
        return true;
    }

    /// <summary>The Paste tool takes the press on the cell under it: the paste is placed there.</summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the Paste tool is in use and took it.</returns>
    private bool PressPaste(Point at)
    {
        if (Mode != ToolMode.Paste)
        {
            return false;
        }
        _dragFrom = null;
        if (GridAt(at, _surfaceSize) is { } cell)
        {
            PasteClicked?.Invoke(cell);
        }
        Wake();
        return true;
    }

    /// <summary>The Area tool takes the press: the area starts at the cell under it.</summary>
    /// <param name="e">The press.</param>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the Area tool is in use and took it.</returns>
    private bool PressArea(PointerPressedEventArgs e, Point at)
    {
        if (Mode != ToolMode.Area)
        {
            return false;
        }
        _dragFrom = null;
        _areaDown = true;
        Area.Down(GridAt(at, _surfaceSize), e.ClickCount);
        Wake();
        return true;
    }

    /// <summary>The Path tool takes the press: a point is put down (or a segment, with the modifier keys).</summary>
    /// <param name="e">The press.</param>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the Path tool is in use and took it.</returns>
    private bool PressPath(PointerPressedEventArgs e, Point at)
    {
        if (Mode != ToolMode.Path)
        {
            return false;
        }
        _dragFrom = null;
        _pathDown = true;
        var mods = e.KeyModifiers;
        Path.Down(GridAt(at, _surfaceSize), at, g => ScreenOfGrid(g), Ctrl(mods), mods.HasFlag(KeyModifiers.Alt),
            mods.HasFlag(KeyModifiers.Shift), GridHeight);
        Wake();
        return true;
    }
}

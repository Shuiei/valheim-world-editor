using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The pointer moving and leaving the surface: the tools that follow the pointer, and the hover looks.
public sealed partial class GlView
{
    /// <summary>
    /// The pointer moved over the surface: the drawing tools follow it, the select, dungeon and measure tools hover,
    /// and the drag moves the view.
    /// </summary>
    /// <param name="surface">The surface the pointer is on.</param>
    /// <param name="e">The move.</param>
    private void OnPointerMoved(Control surface, PointerEventArgs e)
    {
        _modKeys = e.KeyModifiers;
        _pointer = _heldAt = e.GetPosition(surface);
        _surfaceSize = surface.Bounds.Size;
        MoveDrawing(_pointer.Value);
        MoveSelectOrHover(_pointer.Value, e);
        Drag(_pointer.Value);
    }

    /// <summary>Moves the path, area, paste and place tools to the pointer, while each is in use.</summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    private void MoveDrawing(Point at)
    {
        if (_pathDown)
        {
            Path.Moved(GridAt(at, _surfaceSize));
        }
        if (_areaDown)
        {
            Area.Moved(GridAt(at, _surfaceSize));
        }
        if (Mode == ToolMode.Paste)
        {
            Paste.At = GridAt(at, _surfaceSize);
        }
        if (Mode == ToolMode.Place)
        {
            Place?.Moved(at, _surfaceSize);
        }
        if (Mode == ToolMode.Mod)
        {
            PointMod(_modDown ? ValheimWorldEditor.Sdk.PointerAction.Drag : ValheimWorldEditor.Sdk.PointerAction.Move, at, _modKeys);
        }
    }

    /// <summary>
    /// The select drag follows the pointer; with no tool in the select mode the pointer hovers (and queues the look
    /// for the object a click would pick); the dungeon and measure tools hover in their own way.
    /// </summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <param name="e">The move.</param>
    private void MoveSelectOrHover(Point at, PointerEventArgs e)
    {
        if (_selectDown)
        {
            SelectTool.Moved(at, _surfaceSize, Ctrl(e.KeyModifiers));
        }
        else if (SelectMode && Tool == null)
        {
            SelectTool.Hover(at, _surfaceSize);
            // The object a click would pick (none over a move handle), looked for once the moves waiting are
            // taken: a fast mouse sends many a frame, and each look tries every object's box.
            QueueHoverPick();
        }
        else if (Mode == ToolMode.Dungeon && WorldRay(at, _surfaceSize) is { } ray)
        {
            Dungeon.Hover(ray.O, ray.D);
        }
        else if (Mode == ToolMode.Measure && WorldAt(at, _surfaceSize) is { } w)
        {
            Tape.Move(w);
        }
    }

    /// <summary>The pointer left the surface: no hover object, and the view is woken.</summary>
    private void OnPointerExited()
    {
        _pointer = null;
        SetHoverObject(null);
        Wake();
    }
}

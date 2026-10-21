using Avalonia;
using Avalonia.Input;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The button let go: what the press started ends (a path, area, place, select drag or brush stroke), and a click
// in the View tool picks the object under the pointer.
public sealed partial class GlView
{
    /// <summary>
    /// The button let go (or the pointer lost): ends what the press started (a path, area, place, select drag or a
    /// brush stroke) and the view's sliding; a click (not a drag) in the View tool picks the object under the pointer.
    /// Nothing when nothing is held.
    /// </summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <param name="size">The surface's size.</param>
    /// <param name="mods">The keys held.</param>
    /// <param name="click">Whether it is a real release, which may pick (not a lost pointer).</param>
    private void Up(Point at, Size size, KeyModifiers mods, bool click)
    {
        UpMod(at, mods);
        EndDrawing(at, size);
        EndPress(at, size, mods, click);
        _pressAt = null;
        _dragFrom = null;
        _pickSwallow = false;
        Panning = false;
        Wake();
    }

    /// <summary>
    /// Ends what the press started that is not a drawing: the select drag, or the brush stroke. Else a click in the
    /// View tool picks the object under the pointer.
    /// </summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <param name="size">The surface's size.</param>
    /// <param name="mods">The keys held.</param>
    /// <param name="click">Whether it is a real release, which may pick (not a lost pointer).</param>
    private void EndPress(Point at, Size size, KeyModifiers mods, bool click)
    {
        if (_selectDown)
        {
            EndSelectDrag(at, size, mods);
        }
        else if (_brushDown)
        {
            EndBrushStroke();
        }
        else if (click && IsClickInView(at))
        {
            ViewClick(at, size, mods);
        }
    }

    /// <summary>Ends the brush stroke that the press started: its message is passed on.</summary>
    private void EndBrushStroke()
    {
        _brushDown = false;
        var message = Scene?.Session?.EndStroke() ?? Said.Empty;
        StrokeEnded?.Invoke(message);
    }

    /// <summary>Ends the path, area and place drags that the press started, if any.</summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <param name="size">The surface's size.</param>
    private void EndDrawing(Point at, Size size)
    {
        if (_pathDown)
        {
            _pathDown = false;
            Path.Up();
        }
        if (_areaDown)
        {
            _areaDown = false;
            Area.Up();
        }
        if (_placeDown)
        {
            _placeDown = false;
            Place?.Up(at, size);
        }
    }

    /// <summary>Ends the select drag: the release selects, then the handle under the pointer is hovered again.</summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <param name="size">The surface's size.</param>
    /// <param name="mods">The keys held.</param>
    private void EndSelectDrag(Point at, Size size, KeyModifiers mods)
    {
        _selectDown = false;
        SelectTool.Up(at, size, mods);
        // A look queued by a move just before the press was dropped while the button was down. The handle
        // under the pointer is found again first: the drag's moves did not look for it, and over a handle no
        // object is outlined.
        SelectTool.Hover(at, size);
        QueueHoverPick();
    }
}

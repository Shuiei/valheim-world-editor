using Avalonia;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    /// <summary>
    /// A mouse press: picks a kind (Pick from world), picks the elevation (Alt), starts a brush stroke, or starts or
    /// edits a shape.
    /// </summary>
    /// <param name="at">The mouse on the view, in pixels.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="shift">Shift held (Brush: erase; picking: add the kind).</param>
    /// <param name="ctrl">Ctrl held: remove the point clicked.</param>
    /// <param name="alt">Alt held (without Shift): pick the elevation.</param>
    /// <param name="clicks">The click count; a double-click on the last point places the shape.</param>
    public void Down(Point at, Size size, bool shift, bool ctrl, bool alt, int clicks)
    {
        _pointer = at;
        _size = size;
        if (PickOnce is { } pick)
        {
            PickOnce = null;
            if (_view.ObjectAt(at, size) is int i && _view.Scene is { } sc && Tool.NameOf(sc.Things[i].Prefab) is string name)
            {
                pick(name, shift);
            }
            else
            {
                Message?.Invoke(Said.Of("view.msg.nothingPicked"));
            }
            return;
        }
        // Building: where the cursor points (on a piece, or the ground), as the preview shows it.
        var hit = Tool.Building && _at != null ? _at : _view.GridAt(at, size);
        if (alt && !shift)
        {
            if (_view.WorldAt(at, size) is { } w)
            {
                Message?.Invoke(Tool.PickElevation(_view.ObjectAt(at, size), w.Y));
            }
            return;
        }
        if (hit is not { } h)
        {
            return;
        }
        _at = h;
        if (Tool.Mode == PlaceTool.Modes.Brush)
        {
            // The click's placements are taken now, from what the preview shows.
            _stroke = new Stroke { Erase = shift, From = at, Stamp = [.. _preview], Last = _clock.ElapsedMilliseconds };
            _timer.Start();
            return;
        }
        _press = (at, h, false);
        if (Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape != PlaceTool.LineShapes.Points)
        {
            Tool.FigA = h;
            Tool.FigB = null;
            Tool.Notify();
            return;
        }
        DownShape(at, h, ctrl, clicks);
    }
}

using System.Numerics;
using Avalonia;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    /// <summary>
    /// The mouse was let go: ends a stroke (committing it), a point drag, or a press on a shape (adding the point, or
    /// finishing the figure or box).
    /// </summary>
    public void Up(Point at, Size size)
    {
        if (_stroke is { } s)
        {
            _stroke = null;
            _timer.Stop();
            EndStroke(s);
            return;
        }
        if (_dragPt != null)
        {
            _dragPt = null;
            return;
        }
        if (_press is not { } p)
        {
            return;
        }
        _press = null;
        var hit = _view.GridAt(at, size) ?? _at ?? p.Hit;
        if (Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape != PlaceTool.LineShapes.Points)
        {
            if (!p.Drag && Tool.FigB == null)
            {
                Tool.FigA = null;
                Message?.Invoke(Tool.LineShape == PlaceTool.LineShapes.Circle ? Said.Of("place.msg.circleHow") : Said.Of("place.msg.rectHow"));
            }
            Tool.Notify();
            return;
        }
        if (Pointed)
        {
            var q = Tool.Local(hit);
            lock (Tool.Points)
            {
                if (!p.Drag || Vector2.Distance(Tool.Points[^1], q) > 0.3f)
                {
                    Tool.Points.Add(q);
                }
            }
        }
        else if (!p.Drag)
        {
            Tool.GridA = Tool.GridB = null;
        }
        Tool.Notify();
    }
}

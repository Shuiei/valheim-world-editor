using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    /// <summary>
    /// A press that is being dragged: starts the drag past 6 px (adding the press's point to a line or zone), then moves
    /// the grid box's far corner or adds freehand points every 2 m.
    /// </summary>
    /// <param name="at">The mouse on the view, in pixels.</param>
    /// <param name="h">The grid point under the mouse.</param>
    /// <returns>True when the drag took the move.</returns>
    private bool DragPress(Point at, Vector2 h)
    {
        if (_press is { } pr)
        {
            if (!pr.Drag && Point.Distance(at, pr.At) > 6)
            {
                _press = pr with { Drag = true };
                if (Pointed)
                {
                    var q = Tool.Local(pr.Hit);
                    lock (Tool.Points)
                    {
                        if (Tool.Points.Count == 0 || Vector2.Distance(Tool.Points[^1], q) > 0.3f)
                        {
                            Tool.Points.Add(q);
                        }
                    }
                }
            }
            if (_press.Value.Drag)
            {
                if (Tool.Mode == PlaceTool.Modes.Grid)
                {
                    Tool.GridB = h;
                }
                else
                {
                    // Freehand: a point every 2 m, smoothed by the curve.
                    var q = Tool.Local(h);
                    lock (Tool.Points)
                    {
                        if (Vector2.Distance(Tool.Points[^1], q) >= 2)
                        {
                            Tool.Points.Add(q);
                        }
                    }
                }
                Tool.Notify();
                return true;
            }
        }
        return false;
    }
}

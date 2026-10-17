using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>
    /// A mouse press: picks a height with Alt, removes a point with Ctrl, starts dragging a point, inserts a point on
    /// the line, or starts a new point and drawing.
    /// </summary>
    /// <param name="hit">The grid point under the mouse; null off the ground.</param>
    /// <param name="at">The mouse on the view, in pixels.</param>
    /// <param name="screen">Where a grid point is on the view (null behind the camera).</param>
    /// <param name="ctrl">Ctrl held: remove the point under the mouse.</param>
    /// <param name="alt">Alt held: pick the height under the mouse instead.</param>
    /// <param name="shift">With Alt on a Ramp: pick the ramp's end instead of its start.</param>
    /// <param name="heightAt">The ground's height at a grid point.</param>
    public void Down(Vector2? hit, Point at, Func<Vector2, Point?> screen, bool ctrl, bool alt, bool shift, Func<Vector2, float> heightAt)
    {
        if (alt)
        {
            if (hit is { } a)
            {
                float h = MathF.Round(heightAt(a), 1);
                if (Act == Action.Ramp)
                {
                    if (shift)
                    {
                        End = h;
                    }
                    else
                    {
                        Start = h;
                    }

                    RampEdited = true;
                }
                else
                {
                    Height = h;
                }
                Notify();
            }
            return;
        }
        int pt = PointAt(at, screen);
        if (pt >= 0)
        {
            if (ctrl)
            {
                lock (Points) { Points.RemoveAt(pt); }
                Notify();
                return;
            }
            _drag = pt;
            return;
        }
        if (hit is not { } h2)
        {
            return;
        }
        int seg = Points.Count >= 2 ? SegmentAt(at, screen) : -1;
        if (seg >= 0)
        {
            lock (Points) { Points.Insert(seg + 1, h2); }
            _drag = seg + 1;
            Notify();
            return;
        }
        lock (Points) { Points.Add(h2); }
        Drawing = true;
        Notify();
    }
}

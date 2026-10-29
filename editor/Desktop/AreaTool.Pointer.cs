using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    // ---- The mouse and keys.
    /// <summary>
    /// A mouse button pressed on the ground: a box starts there; a polygon gets a point (a new polygon after a
    /// closed one), or closes on a double-click.
    /// </summary>
    /// <param name="hit">The grid point under the mouse, null when off the ground.</param>
    /// <param name="clicks">The click count (2 for a double-click).</param>
    public void Down(Vector2? hit, int clicks)
    {
        if (hit is not { } h)
        {
            return;
        }
        if (Box)
        {
            lock (Points)
            {
                Points.Clear();
                Points.Add(h);
                Points.Add(h);
            }
            Dragging = true;
        }
        else if (clicks >= 2 && Points.Count >= 3)
        {
            // The double-click's first click already added its point.
            Closed = true;
        }
        else
        {
            lock (Points)
            {
                if (Closed)
                {
                    Points.Clear();
                    Closed = false;
                }
                Points.Add(h);
            }
        }
        Notify();
    }

    /// <summary>The mouse moved: a box being dragged follows it to its far corner.</summary>
    public void Moved(Vector2? hit)
    {
        if (Dragging && hit is { } h)
        {
            lock (Points)
            {
                Points[1] = h;
            }
            Notify();
        }
    }

    /// <summary>
    /// The mouse button released: a box ends there, or is dropped if less than a metre across either way (a line has
    /// no area to work on).
    /// </summary>
    public void Up()
    {
        if (!Dragging)
        {
            return;
        }
        Dragging = false;
        if (MathF.Abs(Points[1].X - Points[0].X) < 1 || MathF.Abs(Points[1].Y - Points[0].Y) < 1)
        {
            Clear();
        }
        else
        {
            Notify();
        }
    }
}

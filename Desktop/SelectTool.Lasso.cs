using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The zone drawn as the pointer moves: a point is added once it has moved half a metre on the ground; the view's
    /// copy is made again only when the zone has a new point.
    /// </summary>
    /// <param name="l">The zone as it stands.</param>
    /// <param name="at">The point in the view.</param>
    /// <param name="size">The view's size.</param>
    private void DrawLasso((Point At, List<Vector2> Points, bool Add, bool Drawing) l, Point at, Size size)
    {
        bool drawing = l.Drawing || Point.Distance(at, l.At) > 6;
        bool added = false;
        if (drawing && _view.WorldAt(at, size) is { } g && Vector2.Distance(l.Points[^1], new Vector2(g.X, g.Z)) >= 0.5f)
        {
            l.Points.Add(new Vector2(g.X, g.Z));
            added = true;
        }
        _lasso = l with { Drawing = drawing };
        // The view's copy is made again only when the zone has a new point.
        if (added || Lasso == null)
        {
            Lasso = [.. l.Points];
            _view.LassoChanged();
        }
    }

    /// <summary>Whether a point (x, z) is inside a polygon of (x, z) points (even-odd rule).</summary>
    public static bool Inside(IReadOnlyList<Vector2> poly, float x, float z)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.Y > z) != (b.Y > z) && x < (b.X - a.X) * (z - a.Y) / (b.Y - a.Y) + a.X)
            {
                c = !c;
            }
        }
        return c;
    }
}

using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    // ---- Shapes.
    /// <summary>A press on a shape: where (pixels and grid point) and whether it has become a drag.</summary>
    private (Point At, Vector2 Hit, bool Drag)? _press;
    /// <summary>The shape's point being dragged, or null.</summary>
    private int? _dragPt;

    /// <summary>The shape's points can be dragged, inserted and removed (Line with Points, and Zone).</summary>
    private bool Editable => Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape == PlaceTool.LineShapes.Points || Tool.Mode == PlaceTool.Modes.Zone;
    /// <summary>The shape is made of clicked points (Line and Zone).</summary>
    private bool Pointed => Tool.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone;

    /// <summary>The points where they are drawn (a zone can be turned: its points are kept unturned).</summary>
    public List<Vector2> ShownPoints()
    {
        lock (Tool.Points)
        {
            return Tool.Mode == PlaceTool.Modes.Zone ? [.. Tool.Points.Select(Tool.Xf)] : [.. Tool.Points];
        }
    }

    /// <summary>Where a grid point is on the view (30 cm above the ground), or null behind the camera.</summary>
    private Point? Screen(Vector2 g) => _view.ScreenOfGrid(g, 0.3f);

    /// <summary>The shape's point within 10 px of the mouse, or -1.</summary>
    private int PointAt(Point at) => Lines.PointNear(ShownPoints(), at, Screen);

    /// <summary>The stretch of the outline under the mouse: the index of the point it starts from, or -1.</summary>
    private int SegmentAt(Point at)
    {
        var dense = Tool.Mode == PlaceTool.Modes.Zone
            ? ShownPoints() is { Count: > 0 } z ? [.. z.Append(z[0]).Select((p, i) => (P: p, Seg: i))] : []
            : Tool.LineCurve();
        return Lines.StretchNear(dense, at, Screen);
    }
}

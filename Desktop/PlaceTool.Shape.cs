using System.Numerics;

namespace TerrainEditor.Desktop;

// The line, grid and zone shapes: their points, the turn of a shape, and the draws along it.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Line and Zone: the points clicked, grid points of the block (a zone's kept unturned). Changed under its own
    /// lock: the drawing thread reads it.
    /// </summary>
    public List<Vector2> Points { get; } = [];

    /// <summary>Grid: the box's first corner.</summary>
    public Vector2? GridA { get; set; }

    /// <summary>Grid: the box's opposite corner.</summary>
    public Vector2? GridB { get; set; }

    /// <summary>Circle: its centre; Rectangle: its first corner.</summary>
    public Vector2? FigA { get; set; }

    /// <summary>Circle: a point on its edge; Rectangle: the opposite corner (before end-to-end snapping).</summary>
    public Vector2? FigB { get; set; }

    /// <summary>Shapes: each object's draws, by its index in the shape.</summary>
    private readonly List<Draw> _draws = [];

    /// <summary>The draws of a shape's i-th object, made as needed.</summary>
    private Draw DrawAt(int i)
    {
        while (_draws.Count <= i)
        {
            _draws.Add(NewDraw());
        }
        return _draws[i];
    }

    /// <summary>A point turned by the shape's turn about its pivot.</summary>
    public Vector2 Xf(Vector2 p) => Turn(p, ShapeTurn);

    /// <summary>A point turned back: the opposite of <see cref="Xf"/>.</summary>
    public Vector2 Inv(Vector2 p) => Turn(p, -ShapeTurn);

    /// <summary>A point turned this many degrees (clockwise seen from above) about the shape's pivot; unturned with none.</summary>
    /// <param name="p">The point.</param>
    /// <param name="degrees">The turn.</param>
    private Vector2 Turn(Vector2 p, float degrees)
    {
        if (degrees == 0 || _pivot is not { } c)
        {
            return p;
        }
        float t = degrees * MathF.PI / 180, co = MathF.Cos(t), sn = MathF.Sin(t);
        var d = p - c;
        return new Vector2(c.X + d.X * co + d.Y * sn, c.Y - d.X * sn + d.Y * co);
    }

    /// <summary>A point as kept: zone outlines are kept unturned.</summary>
    public Vector2 Local(Vector2 p) => Mode == Modes.Zone ? Inv(p) : p;

    /// <summary>Turns the zone or the grid box by this many degrees; false when there is none to turn.</summary>
    public bool TurnShape(float degrees)
    {
        var pts = Mode == Modes.Zone ? [.. Points] : GridA is { } a && GridB is { } b ? new List<Vector2> { a, b } : [];
        if (pts.Count == 0)
        {
            return false;
        }
        _pivot ??= new Vector2((pts.Min(p => p.X) + pts.Max(p => p.X)) / 2, (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2);
        ShapeTurn = ((ShapeTurn + degrees + 180) % 360 + 360) % 360 - 180;
        Notify();
        return true;
    }

    /// <summary>A new grid box starts unturned.</summary>
    public void ClearTurn()
    {
        ShapeTurn = 0;
        _pivot = null;
    }

    /// <summary>Removes the line, zone, figure and grid box, and their turn.</summary>
    public void ClearShape()
    {
        lock (Points)
        {
            Points.Clear();
        }
        FigA = FigB = GridA = GridB = null;
        ShapeTurn = 0;
        _pivot = null;
        Notify();
    }

    /// <summary>Turns the rotation by this many degrees (kept within -180 to 180).</summary>
    public void TurnBy(float degrees)
    {
        Rotation = ((Rotation + degrees + 180) % 360 + 360) % 360 - 180;
        Notify();
    }
}

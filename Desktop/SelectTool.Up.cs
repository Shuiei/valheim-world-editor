using Avalonia;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The left button came up: ends a handle drag or a move, selects what is inside the zone drawn, or (a plain
    /// click on a selected object) picks that one.
    /// </summary>
    public void Up(Point at, Size size, bool shift)
    {
        if (_handleDrag != null)
        {
            _handleDrag = null;
            Commit();
            return;
        }
        if (_box is { } b)
        {
            EndBox(b, size);
            return;
        }
        if (_lasso is { } l)
        {
            StopZones();
            if (!l.Drawing || l.Points.Count < 3)
            {
                ClickedEmpty(l.Add);
                return;
            }
            var s = Scene!;
            // Only what is inside the zone's bounding box is tested against its outline.
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in l.Points)
            {
                (minX, maxX, minZ, maxZ) = (MathF.Min(minX, p.X), MathF.Max(maxX, p.X), MathF.Min(minZ, p.Y), MathF.Max(maxZ, p.Y));
            }
            var ids = Shown().Where(i =>
            {
                var pos = s.Things[i].Position;
                return pos.X >= minX && pos.X <= maxX && pos.Z >= minZ && pos.Z <= maxZ && Inside(l.Points, pos.X, pos.Z);
            }).ToList();
            ZoneSelected(ids, l.Add);
            return;
        }
        var d = _drag;
        _drag = null;
        if (d == null)
        {
            return;
        }
        if (_dragMoving)
        {
            Commit();
        }
        else
        {
            // A plain click on a selected object: that one only.
            _view.Pick(at, size, shift, Range);
        }
    }
}

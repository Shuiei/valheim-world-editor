using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// The same room on the other side of its parent, mirrored across the parent's middle; null when it is
    /// not a centred, ordinary side room, its kind comes only once (a kitchen, a forge) or the mirror does not fit.
    /// </summary>
    private DungeonPlan.Space? Twin(DungeonPlan.Level l, DungeonPlan.Space n)
    {
        var parent = n.Parent;
        if (parent == null || SpecOf(n.Room).Unique || n.Shift != 0 || n.Tall || n.Dir == parent.Dir || n.Dir == (-parent.Dir.X, -parent.Dir.Z))
        {
            return null;
        }
        var first = parent.Doors.FirstOrDefault(d => d.A == parent && (d.B == n || d.B.Corridor && d.B.Doors.Any(x => x.B == n)));
        if (first == null || first.B.Corridor && first.B.Shape != DungeonPlan.Shape.Rect)
        {
            return null;
        }
        if (!MirroredSlot(parent, n, first, out var dir, out var o1, out var o2))
        {
            return null;
        }
        var p = TryPlace(l, o1, o2, dir, n.Across, n.Deep, n.Length, 0, aligned: false, tall: false);
        if (p == null)
        {
            return null;
        }
        var tiles = MirroredTiles(parent, n, p, o1, o2, dir);
        var twin = NewSpace(l, n.Room, dir, p.Box, tiles);
        twin.Shape = tiles == null ? DungeonPlan.Shape.Rect : n.Shape;
        twin.Parent = parent;
        Join(l, parent, twin, p, null);
        return twin;
    }
}

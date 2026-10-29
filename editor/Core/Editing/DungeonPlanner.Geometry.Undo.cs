using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>Where the plan of a level is now, to take back what is added after it.</summary>
    private Point Mark(DungeonPlan.Level l) => new(l.Spaces.Count, _plan.Spaces.Count, _plan.Doors.Count, _next, [.. _used]);

    /// <summary>
    /// Takes back every space and doorway added to the level since <paramref name="at"/>: their tiles are free
    /// again, the rooms they opened off lose those doorways, and kinds first made since count as unmade.
    /// </summary>
    private void Drop(DungeonPlan.Level l, Point at)
    {
        if (_plan.Spaces.Count - at.AllSpaces != l.Spaces.Count - at.Spaces)
        {
            throw new InvalidOperationException("spaces were added to another level since the mark");
        }
        var gone = l.Spaces.GetRange(at.Spaces, l.Spaces.Count - at.Spaces);
        foreach (var d in _plan.Doors.GetRange(at.Doors, _plan.Doors.Count - at.Doors))
        {
            d.A.Doors.Remove(d);
            d.B.Doors.Remove(d);
        }
        _plan.Doors.RemoveRange(at.Doors, _plan.Doors.Count - at.Doors);
        foreach (var s in gone)
        {
            foreach (var t in s.Tiles)
            {
                l.Owner.Remove(t);
            }
        }
        l.Spaces.RemoveRange(at.Spaces, gone.Count);
        if (gone.Any(s => s.Tall))
        {
            // Made again from the tall rooms kept: one dropped may share a cell with one kept.
            l.TallCells.Clear();
            l.TallCells.UnionWith(l.Spaces.Where(s => s.Tall).SelectMany(s => s.Tiles).Select(DungeonPlan.CellOf));
        }
        _plan.Spaces.RemoveRange(at.AllSpaces, gone.Count);
        _next = at.Next;
        _used.IntersectWith(at.Used);
    }

    /// <summary>How far the plan had got: what <see cref="Drop"/> takes it back to.</summary>
    /// <param name="Spaces">The level's spaces then.</param>
    /// <param name="AllSpaces">The plan's spaces then.</param>
    /// <param name="Doors">The plan's doorways then.</param>
    /// <param name="Next">The next space's id then.</param>
    /// <param name="Used">The kinds of room made by then.</param>
    private readonly record struct Point(int Spaces, int AllSpaces, int Doors, int Next, Room[] Used);
}

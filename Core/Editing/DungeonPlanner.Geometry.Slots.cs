using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>The tiles along a side of a space: those whose neighbour in dir is outside it, in order along the side.</summary>
    private static List<(int, int)> SideTiles(DungeonPlan.Space s, (int X, int Z) dir)
    {
        var (x, z) = Right(dir);
        return [.. s.Tiles.Where(t => !s.Tiles.Contains(Add(t, dir))).OrderBy(t => t.I * x + t.J * z)];
    }

    /// <summary>Whether two side tiles are next to each other and a doorway may open between them on side dir of s.</summary>
    private static bool IsSlot((int I, int J) t1, (int I, int J) t2, DungeonPlan.Space s, (int X, int Z) dir) =>
        Math.Abs(t1.I - t2.I) + Math.Abs(t1.J - t2.J) == 1 && SlotAllowed(s, t1, t2, dir);

    /// <summary>Whether a doorway may open at tiles t1, t2 on side dir of s.</summary>
    private static bool SlotAllowed(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir)
    {
        if (s.Room == Room.Entrance && dir == (-s.Dir.X, -s.Dir.Z))
        {
            return false; // the entrance's back wall: the portal stands before it
        }
        if (s.Room == Room.StairBottom)
        {
            return StairFootSlot(s, t1, t2, dir);
        }
        if (s.Room == Room.Cells)
        {
            return CellBlockSlot(s, t1, t2, dir);
        }
        return ClearOfDoorways(s, t1, t2, dir);
    }
}

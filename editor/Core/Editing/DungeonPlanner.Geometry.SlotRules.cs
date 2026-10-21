namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Whether a doorway may open on the foot of a stairwell: forward, or on the sides of its last 2 m rows; never at its back.
    /// </summary>
    private static bool StairFootSlot(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir)
    {
        if (dir == s.Dir)
        {
            return true;
        }
        if (dir == (-s.Dir.X, -s.Dir.Z))
        {
            return false;
        }
        int Depth((int I, int J) t) => (t.I - s.X0) * s.Dir.X + (t.J - s.Z0) * s.Dir.Z + (s.Dir.X < 0 ? s.W - 1 : 0) + (s.Dir.Z < 0 ? s.H - 1 : 0);
        return Depth(t1) >= s.Deep - 2 && Depth(t2) >= s.Deep - 2;
    }

    /// <summary>Whether a doorway may open on a cell block: only at the ends of its aisle, on the middle tiles.</summary>
    private static bool CellBlockSlot(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir)
    {
        if (dir != s.Dir && dir != (-s.Dir.X, -s.Dir.Z))
        {
            return false;
        }
        int Across((int I, int J) t) => s.Dir.Z != 0 ? t.I - s.X0 : t.J - s.Z0;
        int mid = s.Across / 2;
        return Math.Min(Across(t1), Across(t2)) == mid - 1;
    }

    /// <summary>
    /// Whether a doorway may open at tiles t1, t2 without being on another doorway's tiles or right next to them (a corner
    /// is allowed: the doorway may open flush with it).
    /// </summary>
    private static bool ClearOfDoorways(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir)
    {
        foreach (var d in s.Doors)
        {
            var mine = d.A == s ? [d.T1, d.T2] : new[] { Add(d.T1, d.Dir), Add(d.T2, d.Dir) };
            var side = d.A == s ? d.Dir : (-d.Dir.X, -d.Dir.Z);
            if (side == dir && mine.Any(m => Math.Abs(m.Item1 - t1.I) + Math.Abs(m.Item2 - t1.J) <= 2 || Math.Abs(m.Item1 - t2.I) + Math.Abs(m.Item2 - t2.J) <= 2))
            {
                return false;
            }
        }
        return true;
    }
}

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Whether a room's box may go there: whole cells when aligned, every cell free, and, when tall, nothing
    /// on the level above in its cells.
    /// </summary>
    private bool BoxFits(DungeonPlan.Level l, (int, int, int, int) box, bool aligned, bool tall)
    {
        if (aligned && !IsWholeCells(box))
        {
            return false;
        }
        foreach (var t in Box(box))
        {
            if (!Free(l, t))
            {
                return false;
            }
        }
        return !(tall && l.Index > 0) || ClearAbove(_plan, l, box);
    }

    /// <summary>Whether a box covers whole cells: every side a multiple of 4 tiles.</summary>
    internal static bool IsWholeCells((int, int, int, int) box) =>
        box.Item1 % 4 == 0 && box.Item2 % 4 == 0 && box.Item3 % 4 == 0 && box.Item4 % 4 == 0;

    /// <summary>Whether nothing on the level above lies in the cells of a tall room's box (two floors high).</summary>
    internal static bool ClearAbove(DungeonPlan plan, DungeonPlan.Level l, (int, int, int, int) box)
    {
        var above = plan.Levels[l.Index - 1];
        foreach (var (ci, cj) in Box(box).Select(DungeonPlan.CellOf).ToHashSet())
        {
            for (int k = 0; k < 16; k++)
            {
                if (above.Owner.ContainsKey((4 * ci + k % 4, 4 * cj + k / 4)))
                {
                    return false;
                }
            }
        }
        return true;
    }
}

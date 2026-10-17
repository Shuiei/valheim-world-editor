using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>Joins a placed room to its parent: wall to wall, or by a new corridor with a doorway at each end.</summary>
    private void Join(DungeonPlan.Level l, DungeonPlan.Space parent, DungeonPlan.Space n, Placement p, Door? door)
    {
        n.Shift = p.Shift;
        n.Length = p.Length;
        if (p.Length == 0)
        {
            Connect(parent, n, p.T1, p.T2, p.Dir, door);
            return;
        }
        var c = NewCorridor(l, p.Corridor, p.Dir);
        c.Parent = parent;
        if (p.EndDir != p.Dir)
        {
            // A bent corridor: its box is more than its tiles.
            c.Shape = DungeonPlan.Shape.L;
        }
        else if (p.Length >= 3 && _rnd.NextDouble() < 0.35)
        {
            Recesses(l, c, 1, _rnd.Next(1, 3));
        }
        Connect(parent, c, p.T1, p.T2, p.Dir, door == Door.Secret ? Door.Secret : null);
        Connect(c, n, p.E1, p.E2, p.EndDir, door == Door.Secret ? Door.Open : door);
    }
}

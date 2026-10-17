namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Collects the joins a room may make off one of its sides in direction <paramref name="dir"/>: each slot along the
    /// side, with a corridor of up to six tiles to a room of the level that lies across.
    /// </summary>
    /// <param name="l">The level.</param>
    /// <param name="a">The room the joins leave from.</param>
    /// <param name="dir">The side to leave by.</param>
    /// <param name="dist">The room's distances, worked out only when a join is found.</param>
    /// <param name="options">The joins collected so far, each with its score and how to make it.</param>
    private void AddLoopOptionsAlong(DungeonPlan.Level l, DungeonPlan.Space a, (int X, int Z) dir, Lazy<Dictionary<DungeonPlan.Space, int>> dist,
        List<(float Score, Action Make)> options)
    {
        var side = SideTiles(a, dir);
        for (int at = 0; at + 1 < side.Count; at++)
        {
            var (t1, t2) = (side[at], side[at + 1]);
            if (!IsSlot(t1, t2, a, dir))
            {
                continue;
            }
            for (int len = 0; len <= 6; len++)
            {
                var n1 = Add(t1, dir, len + 1);
                var n2 = Add(t2, dir, len + 1);
                var b = l.Owner.GetValueOrDefault(n1);
                if (b != null && b == l.Owner.GetValueOrDefault(n2) && b != a && Loopable(b))
                {
                    int d = dist.Value.GetValueOrDefault(b, 99);
                    var path = Enumerable.Range(1, len).SelectMany(t => new[] { Add(t1, dir, t), Add(t2, dir, t) }).ToList();
                    if (d >= 3 && path.All(t => Free(l, t)) && SlotAllowed(b, n1, n2, (-dir.X, -dir.Z)))
                    {
                        options.Add((d - len * 0.3f + (float)_rnd.NextDouble(), LoopJoinAction(l, a, b, t1, t2, dir, len, path)));
                    }
                    break;
                }
                if (b != null || !Free(l, n1) || !Free(l, n2))
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The action that makes one loop join: a doorway straight from a to b when len is 0, otherwise through a new
    /// corridor over <paramref name="path"/>. The arguments are copied, so the action keeps this join's own values.
    /// </summary>
    private Action LoopJoinAction(DungeonPlan.Level l, DungeonPlan.Space a, DungeonPlan.Space b, (int, int) t1, (int, int) t2, (int X, int Z) dir, int len,
        List<(int, int)> path) =>
        () => MakeLoop(l, a, b, t1, t2, dir, len, path);

}


namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Joins rooms that are far apart on the way but close in space: through a shared wall, or by a
    /// short straight corridor. How many follows the settings' Loops and the level's rooms.
    /// </summary>
    private void AddLoops(DungeonPlan.Level l)
    {
        int want = (int)MathF.Round(_s.Loops * l.Spaces.Count(s => s.IsRoom) / 5f);
        for (int made = 0, tries = 0; made < want && tries < want * 3 + 3; tries++)
        {
            // Joins change the ways between rooms, so the distances are worked out again each try: the stairs once,
            // and each room's only when it has a room to join.
            var stairs = StairLinks(_plan.Spaces);
            var options = new List<(float Score, Action Make)>();
            foreach (var a in l.Spaces.Where(Loopable))
            {
                var dist = new Lazy<Dictionary<DungeonPlan.Space, int>>(() => Distances(a, stairs));
                foreach (var dir in Dirs)
                {
                    AddLoopOptionsAlong(l, a, dir, dist, options);
                }
            }
            if (options.Count == 0)
            {
                break;
            }
            options.OrderByDescending(o => o.Score).First().Make();
            made++;
        }
    }

    /// <summary>
    /// Makes a loop join: a doorway from a to b when len is 0, otherwise a new corridor over <paramref name="path"/>
    /// with a doorway at each end.
    /// </summary>
    private void MakeLoop(DungeonPlan.Level l, DungeonPlan.Space a, DungeonPlan.Space b, (int, int) t1, (int, int) t2, (int X, int Z) dir, int len,
        List<(int, int)> path)
    {
        if (len == 0)
        {
            Connect(a, b, t1, t2, dir, loop: true);
            return;
        }
        var c = NewCorridor(l, path, dir);
        Connect(a, c, t1, t2, dir, loop: true);
        Connect(c, b, Add(t1, dir, len), Add(t2, dir, len), dir, loop: true);
    }
}

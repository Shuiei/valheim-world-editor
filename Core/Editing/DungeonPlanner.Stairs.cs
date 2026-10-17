using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Stairs down: an aligned 1 × 3 cells room (the first cell to stand in, then the stairwell), and on
    /// the level below the 1 × 2 cells under the stairwell.
    /// </summary>
    /// <returns>The stairs' top, or null when there is no room for them off parent.</returns>
    private DungeonPlan.Space? AttachStairs(DungeonPlan.Level l, DungeonPlan.Space parent)
    {
        if (!CanParent(parent))
        {
            return null;
        }
        var below = _plan.Levels[l.Index + 1];
        // The search off one side gives the same answer each time: a side already tried is passed over (its draw
        // still made, for the same dungeon from the same seed).
        var tried = new List<(int X, int Z)>(4);
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var dir = attempt < 12 ? parent.Dir : Dirs[_rnd.Next(4)];
            if ((dir == (-parent.Dir.X, -parent.Dir.Z) && parent.Room != Room.Hub) || tried.Contains(dir))
            {
                continue;
            }
            tried.Add(dir);
            var side = SideTiles(parent, dir);
            for (int at = 0; at + 1 < side.Count; at++)
            {
                var (t1, t2) = (side[at], side[at + 1]);
                if (!IsSlot(t1, t2, parent, dir))
                {
                    continue;
                }
                for (int n = 0; n < 3 * (_style.CorridorMax + 4); n++)
                {
                    int len = Math.Max(1, _style.CorridorMin) + n / 3, shift = n % 3 - 1;
                    var p = TryPlace(l, t1, t2, dir, 4, 12, len, shift, aligned: true, tall: false);
                    if (p == null)
                    {
                        continue;
                    }
                    var deep = Box(p.Box).Where(t => Depth(p.Box, dir, t) >= 4).ToList();
                    if (deep.Any(below.Owner.ContainsKey))
                    {
                        continue;
                    }
                    var top = NewSpace(l, Room.StairTop, dir, p.Box);
                    top.Parent = parent;
                    top.Main = true;
                    Join(l, parent, top, p, null);
                    foreach (var t in deep)
                    {
                        l.Holes.Add(DungeonPlan.CellOf(t));
                        below.Open.Add(DungeonPlan.CellOf(t));
                    }
                    int x0 = deep.Min(t => t.Item1), z0 = deep.Min(t => t.Item2);
                    var foot = NewSpace(below, Room.StairBottom, dir, (x0, z0, deep.Max(t => t.Item1) - x0 + 1, deep.Max(t => t.Item2) - z0 + 1), deep);
                    foot.Parent = top;
                    foot.Main = true;
                    // The stairs join them (no doorway: the stairwell).
                    return top;
                }
            }
        }
        return null;
    }

    /// <summary>How far tile t is from the back of a box entered along dir (tiles).</summary>
    public static int Depth((int X0, int Z0, int W, int H) box, (int X, int Z) dir, (int I, int J) t) =>
        dir.X > 0 ? t.I - box.X0 : dir.X < 0 ? box.X0 + box.W - 1 - t.I : dir.Z > 0 ? t.J - box.Z0 : box.Z0 + box.H - 1 - t.J;

    /// <summary>
    /// The spaces the stairs join each space to, in the plan's order: a stair foot's top (its parent), and a space's
    /// stair feet (the feet it is the parent of). Spaces without stairs are missing.
    /// </summary>
    /// <param name="spaces">The plan's spaces.</param>
    internal static Dictionary<DungeonPlan.Space, List<DungeonPlan.Space>> StairLinks(IReadOnlyList<DungeonPlan.Space> spaces)
    {
        var feetOf = new Dictionary<DungeonPlan.Space, List<DungeonPlan.Space>>();
        foreach (var o in spaces)
        {
            if (o.Room == Room.StairBottom && o.Parent is { } p)
            {
                (feetOf.TryGetValue(p, out var feet) ? feet : feetOf[p] = []).Add(o);
            }
        }
        var links = new Dictionary<DungeonPlan.Space, List<DungeonPlan.Space>>();
        void Link(DungeonPlan.Space s, DungeonPlan.Space o) => (links.TryGetValue(s, out var l) ? l : links[s] = []).Add(o);
        foreach (var o in spaces)
        {
            if (o.Room == Room.StairBottom && o.Parent is { } p)
            {
                Link(p, o);
            }
            foreach (var foot in feetOf.GetValueOrDefault(o) ?? [])
            {
                Link(foot, o);
            }
        }
        return links;
    }

    /// <summary>
    /// How many rooms away each space is from <paramref name="from"/>, through doorways and stairs
    /// (corridors do not count); spaces not reached are missing.
    /// </summary>
    /// <param name="from">The space to count from.</param>
    /// <param name="stairs">The plan's <see cref="StairLinks"/> as it stands.</param>
    private static Dictionary<DungeonPlan.Space, int> Distances(DungeonPlan.Space from, Dictionary<DungeonPlan.Space, List<DungeonPlan.Space>> stairs)
    {
        var dist = new Dictionary<DungeonPlan.Space, int> { [from] = 0 };
        var queue = new Queue<DungeonPlan.Space>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            var next = s.Doors.Select(d => d.Other(s)).ToList();
            // The stairs join a stairwell's two rooms.
            next.AddRange(stairs.GetValueOrDefault(s) ?? []);
            foreach (var o in next)
            {
                if (!dist.ContainsKey(o))
                {
                    dist[o] = dist[s] + (o.Corridor ? 0 : 1);
                    queue.Enqueue(o);
                }
            }
        }
        return dist;
    }

    /// <summary>Rooms a loop may join: not the arena, treasury, hidden caches or stairs.</summary>
    private static bool Loopable(DungeonPlan.Space s) => s.IsRoom && s.Room is not (Room.Boss or Room.Treasury or Room.Secret or Room.StairTop or Room.StairBottom);
}

using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
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
}

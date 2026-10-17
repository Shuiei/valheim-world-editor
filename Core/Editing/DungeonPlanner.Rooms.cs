using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Adds up to count recesses to a space: a run of width tiles pushed out of one of its walls, where the tiles
    /// beyond are free (an alcove off a room, a niche off a corridor). The space's tiles are then more than its box
    /// says, so it is marked shaped; its box grows to hold them, and <see cref="DungeonPlan.Space.Before"/> keeps the
    /// room's own.
    /// </summary>
    private void Recesses(DungeonPlan.Level l, DungeonPlan.Space s, int width, int count)
    {
        var dirs = Dirs.Where(d => d != s.Dir && d != (-s.Dir.X, -s.Dir.Z)).OrderBy(_ => _rnd.Next()).ToList();
        int made = 0;
        foreach (var dir in dirs)
        {
            var side = SideTiles(s, dir);
            for (int tries = 0; tries < 6 && made < count && side.Count >= width + 2; tries++)
            {
                int at = _rnd.Next(1, side.Count - width);
                var run = side.Skip(at).Take(width).ToList();
                // Adjacent along the side, on one line, their outside tiles free, nowhere near a doorway.
                bool straight = run.Zip(run.Skip(1)).All(p => Math.Abs(p.First.Item1 - p.Second.Item1) + Math.Abs(p.First.Item2 - p.Second.Item2) == 1);
                var outside = run.Select(t => Add(t, dir)).ToList();
                if (!straight || outside.Any(t => !Free(l, t)) || outside.Any(t => Dirs.Any(d => l.Owner.GetValueOrDefault(Add(t, d)) is { } o && o != s)))
                {
                    continue;
                }
                if (s.Doors.Any(d => run.Any(t => Math.Abs(t.Item1 - d.T1.I) + Math.Abs(t.Item2 - d.T1.J) <= 2 || Math.Abs(t.Item1 - d.T2.I) + Math.Abs(t.Item2 - d.T2.J) <= 2)))
                {
                    continue;
                }
                foreach (var t in outside)
                {
                    s.Tiles.Add(t);
                    l.Owner[t] = s;
                }
                s.Recesses.Add((outside, dir));
                s.Before ??= (s.X0, s.Z0, s.W, s.H);
                int x0 = Math.Min(s.X0, outside.Min(t => t.Item1)), z0 = Math.Min(s.Z0, outside.Min(t => t.Item2));
                int x1 = Math.Max(s.X0 + s.W - 1, outside.Max(t => t.Item1)), z1 = Math.Max(s.Z0 + s.H - 1, outside.Max(t => t.Item2));
                (s.X0, s.Z0, s.W, s.H) = (x0, z0, x1 - x0 + 1, z1 - z0 + 1);
                if (s.Shape == DungeonPlan.Shape.Rect)
                {
                    s.Shape = DungeonPlan.Shape.L;
                }
                made++;
            }
        }
    }

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
        var (x0, z0, w, h) = parent.Own;
        (int, int) Mirror((int I, int J) t) => n.Dir.X != 0 ? (2 * x0 + w - 1 - t.I, t.J) : (t.I, 2 * z0 + h - 1 - t.J);
        var dir = (-n.Dir.X, -n.Dir.Z);
        var side = SideTiles(parent, dir);
        var m1 = Mirror(first.T1);
        var m2 = Mirror(first.T2);
        if (!side.Contains(m1) || !side.Contains(m2))
        {
            return null;
        }
        var (o1, o2) = side.IndexOf(m1) < side.IndexOf(m2) ? (m1, m2) : (m2, m1);
        if (!SlotAllowed(parent, o1, o2, dir))
        {
            return null;
        }
        var p = TryPlace(l, o1, o2, dir, n.Across, n.Deep, n.Length, 0, aligned: false, tall: false);
        if (p == null)
        {
            return null;
        }
        // The same shape, mirrored across the parent (an L becomes its mirror image).
        var tiles = n.Shape == DungeonPlan.Shape.Rect ? null : n.Tiles.Select(Mirror).ToList();
        if (tiles != null && (tiles.Any(t => !Box(p.Box).Contains(t)) || !tiles.Contains(Add(o1, dir, p.Length + 1)) || !tiles.Contains(Add(o2, dir, p.Length + 1))))
        {
            tiles = null;
        }
        var twin = NewSpace(l, n.Room, dir, p.Box, tiles);
        twin.Shape = tiles == null ? DungeonPlan.Shape.Rect : n.Shape;
        twin.Parent = parent;
        Join(l, parent, twin, p, null);
        return twin;
    }
}

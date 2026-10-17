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
}

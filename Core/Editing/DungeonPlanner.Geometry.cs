using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>The squared distance between two spaces' middles (square metres).</summary>
    private static float Dist2(DungeonPlan.Space a, DungeonPlan.Space b) =>
        (a.CenterX - b.CenterX) * (a.CenterX - b.CenterX) + (a.CenterZ - b.CenterZ) * (a.CenterZ - b.CenterZ);

    /// <summary>A box of tiles from its corner and size.</summary>
    private static (int X0, int Z0, int W, int H) Rect(int x0, int z0, int w, int h) => (x0, z0, w, h);
    /// <summary>
    /// Adds a space of a kind of room to the level and the plan, taking its tiles (the whole box unless
    /// <paramref name="tiles"/> are given) and marking the kind as used.
    /// </summary>
    private DungeonPlan.Space NewSpace(DungeonPlan.Level l, Room room, (int, int) dir, (int X0, int Z0, int W, int H) box, IEnumerable<(int, int)>? tiles = null)
    {
        var s = new DungeonPlan.Space
        {
            Id = _next++,
            Level = l.Index,
            Room = room,
            Name = SpecOf(room).Name,
            Dir = dir,
            X0 = box.X0,
            Z0 = box.Z0,
            W = box.W,
            H = box.H,
        };
        foreach (var t in tiles ?? Box(box))
        {
            s.Tiles.Add(t);
            l.Owner[t] = s;
        }
        l.Spaces.Add(s);
        _plan.Spaces.Add(s);
        _used.Add(room);
        return s;
    }
    /// <summary>Every tile of a box.</summary>
    private static IEnumerable<(int, int)> Box((int X0, int Z0, int W, int H) b)
    {
        for (int i = b.X0; i < b.X0 + b.W; i++)
        {
            for (int j = b.Z0; j < b.Z0 + b.H; j++)
            {
                yield return (i, j);
            }
        }
    }

    /// <summary>Adds a corridor over the given tiles, its box their bounds.</summary>
    private DungeonPlan.Space NewCorridor(DungeonPlan.Level l, List<(int, int)> tiles, (int, int) dir)
    {
        int x0 = tiles.Min(t => t.Item1), z0 = tiles.Min(t => t.Item2);
        var c = NewSpace(l, Room.Hub, dir, (x0, z0, tiles.Max(t => t.Item1) - x0 + 1, tiles.Max(t => t.Item2) - z0 + 1), tiles);
        c.Corridor = true;
        c.Name = "Corridor";
        return c;
    }

    /// <summary>Adds a doorway from a to b at a's tiles t1, t2 (open unless a kind is given) to both spaces and the plan.</summary>
    private DungeonPlan.Doorway Connect(DungeonPlan.Space a, DungeonPlan.Space b, (int, int) t1, (int, int) t2, (int X, int Z) dir, Door? kind = null, bool loop = false)
    {
        var d = new DungeonPlan.Doorway { A = a, B = b, T1 = t1, T2 = t2, Dir = dir, Kind = kind ?? Door.Open, Loop = loop };
        a.Doors.Add(d);
        b.Doors.Add(d);
        _plan.Doors.Add(d);
        return d;
    }
    /// <summary>
    /// Whether a tile is not taken on the level, lies within the reach of the origin (more room ahead than behind) and
    /// is not over a tall room of the level below, which rises through this one.
    /// </summary>
    private bool Free(DungeonPlan.Level l, (int I, int J) t) => !l.Owner.ContainsKey(t) && Math.Abs(t.I) <= _extent && t.J >= -_extent && t.J <= 2 * _extent && !OverTall(l, t);

    /// <summary>
    /// Whether a tile of the level lies in a cell a tall room of the level below rises through. The levels are laid out
    /// from the first down, so a tall room finds the level above it complete, but the hidden caches come last, on any
    /// level: one over a tall room would floor its upper half.
    /// </summary>
    private bool OverTall(DungeonPlan.Level l, (int I, int J) t) => l.Index + 1 < _plan.Levels.Count && _plan.Levels[l.Index + 1].TallCells.Contains(DungeonPlan.CellOf(t));
}

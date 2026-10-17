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

    /// <summary>How far the plan had got: what <see cref="Drop"/> takes it back to.</summary>
    /// <param name="Spaces">The level's spaces then.</param>
    /// <param name="AllSpaces">The plan's spaces then.</param>
    /// <param name="Doors">The plan's doorways then.</param>
    /// <param name="Next">The next space's id then.</param>
    /// <param name="Used">The kinds of room made by then.</param>
    private readonly record struct Point(int Spaces, int AllSpaces, int Doors, int Next, Room[] Used);

    /// <summary>Where the plan of a level is now, to take back what is added after it.</summary>
    private Point Mark(DungeonPlan.Level l) => new(l.Spaces.Count, _plan.Spaces.Count, _plan.Doors.Count, _next, [.. _used]);

    /// <summary>
    /// Takes back every space and doorway added to the level since <paramref name="at"/>: their tiles are free
    /// again, the rooms they opened off lose those doorways, and kinds first made since count as unmade.
    /// </summary>
    private void Drop(DungeonPlan.Level l, Point at)
    {
        if (_plan.Spaces.Count - at.AllSpaces != l.Spaces.Count - at.Spaces)
        {
            throw new InvalidOperationException("spaces were added to another level since the mark");
        }
        var gone = l.Spaces.GetRange(at.Spaces, l.Spaces.Count - at.Spaces);
        foreach (var d in _plan.Doors.GetRange(at.Doors, _plan.Doors.Count - at.Doors))
        {
            d.A.Doors.Remove(d);
            d.B.Doors.Remove(d);
        }
        _plan.Doors.RemoveRange(at.Doors, _plan.Doors.Count - at.Doors);
        foreach (var s in gone)
        {
            foreach (var t in s.Tiles)
            {
                l.Owner.Remove(t);
            }
        }
        l.Spaces.RemoveRange(at.Spaces, gone.Count);
        if (gone.Any(s => s.Tall))
        {
            // Made again from the tall rooms kept: one dropped may share a cell with one kept.
            l.TallCells.Clear();
            l.TallCells.UnionWith(l.Spaces.Where(s => s.Tall).SelectMany(s => s.Tiles).Select(DungeonPlan.CellOf));
        }
        _plan.Spaces.RemoveRange(at.AllSpaces, gone.Count);
        _next = at.Next;
        _used.IntersectWith(at.Used);
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

    /// <summary>Rooms that can lead on to others.</summary>
    private static bool CanParent(DungeonPlan.Space s) => s.IsRoom && s.Room is not (Room.StairTop or Room.Boss or Room.Treasury or Room.Secret);

    /// <summary>Rooms that can lead on to a room of kind <paramref name="child"/>: the arena leads only to its treasury.</summary>
    private static bool CanParent(DungeonPlan.Space s, Room child) => CanParent(s) || s.Room == Room.Boss && child == Room.Treasury;

    /// <summary>A random space of the level that can lead on (on the main way if <paramref name="main"/>), else its first.</summary>
    private DungeonPlan.Space AnyParent(DungeonPlan.Level l, bool main) =>
        l.Spaces.Where(s => CanParent(s) && (!main || s.Main)).OrderBy(_ => _rnd.Next()).FirstOrDefault() ?? l.Spaces[0];

    /// <summary>The kinds of room that belong beside a kind: what a wing is made of.</summary>
    public static Room[] Kin(Room room) => room switch
    {
        Room.Kitchen => [Room.Feast, Room.Store],
        Room.Feast => [Room.Kitchen, Room.Store, Room.Quarters],
        Room.Store => [Room.Kitchen, Room.Forge, Room.Workshop, Room.Barracks, Room.Mine],
        Room.Barracks => [Room.Guard, Room.Armory, Room.Feast, Room.Store],
        Room.Guard => [Room.Cells, Room.Barracks, Room.Armory],
        Room.Armory => [Room.Barracks, Room.Guard, Room.Forge],
        Room.Cells => [Room.Torture, Room.Guard],
        Room.Torture => [Room.Cells, Room.Guard],
        Room.Library => [Room.Quarters, Room.Chapel, Room.Workshop],
        Room.Quarters => [Room.Library, Room.Kitchen, Room.Feast],
        Room.Chapel => [Room.Shrine, Room.Tomb, Room.Library],
        Room.Shrine => [Room.Chapel, Room.Tomb, Room.Ossuary],
        Room.Burial => [Room.Ossuary, Room.Tomb, Room.Shrine],
        Room.Ossuary => [Room.Burial, Room.Tomb],
        Room.Tomb => [Room.Burial, Room.Shrine, Room.Ossuary],
        Room.Forge => [Room.Workshop, Room.Store, Room.Mine, Room.Armory],
        Room.Workshop => [Room.Forge, Room.Store, Room.Library],
        Room.Mine => [Room.Workshop, Room.Store, Room.Forge],
        Room.Den => [Room.Totem, Room.Store, Room.Den],
        Room.Totem => [Room.Den, Room.Cells],
        _ => [],
    };

    /// <summary>A space to put a side room off, weighted towards crossings and long halls; null when none can lead on.</summary>
    private DungeonPlan.Space? SideParent(DungeonPlan.Level l)
    {
        var options = l.Spaces.Where(s => CanParent(s) && s.Room != Room.Antechamber)
            .Select(s => (s, s.Room switch { Room.Hub => 3f, Room.Hall or Room.Gallery or Room.Mine => 2.5f, Room.StairBottom => 0.5f, _ => s.Main ? 1.5f : 0.6f }))
            .ToList();
        return options.Count == 0 ? null : Pick(options);
    }

    /// <summary>The tiles along a side of a space: those whose neighbour in dir is outside it, in order along the side.</summary>
    private static List<(int, int)> SideTiles(DungeonPlan.Space s, (int X, int Z) dir)
    {
        var (x, z) = Right(dir);
        return [.. s.Tiles.Where(t => !s.Tiles.Contains(Add(t, dir))).OrderBy(t => t.I * x + t.J * z)];
    }

    /// <summary>Whether two side tiles are next to each other and a doorway may open between them on side dir of s.</summary>
    private static bool IsSlot((int I, int J) t1, (int I, int J) t2, DungeonPlan.Space s, (int X, int Z) dir) =>
        Math.Abs(t1.I - t2.I) + Math.Abs(t1.J - t2.J) == 1 && SlotAllowed(s, t1, t2, dir);

    /// <summary>Whether a doorway may open at tiles t1, t2 on side dir of s.</summary>
    private static bool SlotAllowed(DungeonPlan.Space s, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir)
    {
        if (s.Room == Room.Entrance && dir == (-s.Dir.X, -s.Dir.Z))
        {
            return false; // the entrance's back wall: the portal stands before it
        }
        if (s.Room == Room.StairBottom)
        {
            // The foot of the stairs: forward, or the sides of its last 2 m rows.
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
        if (s.Room == Room.Cells)
        {
            // A cell block opens only at the ends of its aisle.
            if (dir != s.Dir && dir != (-s.Dir.X, -s.Dir.Z))
            {
                return false;
            }
            int Across((int I, int J) t) => s.Dir.Z != 0 ? t.I - s.X0 : t.J - s.Z0;
            int mid = s.Across / 2;
            return Math.Min(Across(t1), Across(t2)) == mid - 1;
        }
        // Not on another doorway's tiles or right next to them (a corner is allowed: the doorway may open flush with it).
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

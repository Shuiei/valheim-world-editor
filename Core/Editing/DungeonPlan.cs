using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

/// <summary>
/// The layout of a generated dungeon: levels of rooms and corridors on a 2 m grid (tiles), and the
/// doorways between them. Rooms are boxes, entered in the middle of a side (Dir: the way in) and
/// joined to the room they come from wall to wall or by a straight corridor, so the plan reads like
/// a building: axes, wings, twins on both sides; some take another shape within their box (a corner
/// cut away, the corners notched, an apse). Then loops: rooms far apart on the way that happen to
/// lie close are joined, so there is more than one way round (Jaquays). Floor and roof slabs cover
/// whole 8 m cells, so what needs a cell to itself (a stairwell, a hall two floors high) is aligned to
/// cells and keeps them. Level k's floor is 6k m below the first.
/// </summary>
internal sealed class DungeonPlan
{
    /// <summary>The shape of a room's floor within its box.</summary>
    public enum Shape
    {
        /// <summary>The whole box.</summary>
        Rect,
        /// <summary>The four corners notched: a cross, an octagon's feel.</summary>
        Cross,
        /// <summary>The two far corners notched: the far end narrows like an apse.</summary>
        Apse,
        /// <summary>One near corner cut away: an L.</summary>
        L,
        /// <summary>Both near corners cut away: a neck at the way in, then the full width.</summary>
        T,
    }

    /// <summary>A room or a corridor of the plan.</summary>
    public sealed class Space
    {
        /// <summary>Its floor's shape within its box (a corridor: whatever its tiles are).</summary>
        public Shape Shape;
        /// <summary>A floor finish of its own over the marble (null: the material's, if any), and how far its top is above the marble.</summary>
        public string? Floor;
        public float FloorTop;
        /// <summary>Its number in the order spaces were made.</summary>
        public int Id;
        /// <summary>The level it is on, 0 the first.</summary>
        public int Level;
        /// <summary>What kind of room it is (a corridor is a Hub with <see cref="Corridor"/> set).</summary>
        public Room Room;
        /// <summary>As shown on the plan's map and in the notes.</summary>
        public string Name = "";
        /// <summary>A corridor rather than a room.</summary>
        public bool Corridor;
        /// <summary>Rises through two floors (aligned to cells, nothing above it).</summary>
        public bool Tall;
        /// <summary>On the main way through (the entrance, the way's main rooms, both ends of the stairs).</summary>
        public bool Main;
        /// <summary>Holds the chest with the boss gate's key.</summary>
        public bool KeyChest;
        /// <summary>The tiles it covers (for a room, its whole box).</summary>
        public readonly HashSet<(int I, int J)> Tiles = [];
        /// <summary>A corridor that leads nowhere: one doorway, a cache at its far end.</summary>
        public bool DeadEnd;
        /// <summary>Its recesses (alcoves off a room, niches off a corridor): each one's tiles and the way out of the space into it.</summary>
        public readonly List<(List<(int I, int J)> Tiles, (int X, int Z) Out)> Recesses = [];
        /// <summary>Its box (tiles, world axes).</summary>
        public int X0, Z0, W, H;
        /// <summary>The way in.</summary>
        public (int X, int Z) Dir;
        /// <summary>The doorways on its walls, to either side.</summary>
        public readonly List<Doorway> Doors = [];
        /// <summary>The space it was placed off (null for the entrance).</summary>
        public Space? Parent;
        /// <summary>How it was placed off its parent: shifted across, corridor tiles.</summary>
        public int Shift, Length;

        /// <summary>Whether it is a room (not a corridor).</summary>
        public bool IsRoom => !Corridor;

        /// <summary>The middle of its box along x (metres).</summary>
        public float CenterX => (X0 + W / 2f) * 2;

        /// <summary>The middle of its box along z (metres).</summary>
        public float CenterZ => (Z0 + H / 2f) * 2;

        /// <summary>Tiles across (perpendicular to Dir).</summary>
        public int Across => Dir.X != 0 ? H : W;

        /// <summary>Tiles deep (along Dir).</summary>
        public int Deep => Dir.X != 0 ? W : H;

        /// <summary>The tile at (a across from the left of the way in, b deep from the way in) of its box.</summary>
        public (int, int) TileAt(int a, int b) => Dir switch
        {
            (0, 1) => (X0 + a, Z0 + b),
            (0, -1) => (X0 + W - 1 - a, Z0 + H - 1 - b),
            (1, 0) => (X0 + b, Z0 + H - 1 - a),
            _ => (X0 + W - 1 - b, Z0 + a),
        };

        /// <summary>Whether a tile is one of its own.</summary>
        public bool Has((int, int) t) => Tiles.Contains(t);
    }

    /// <summary>A 4 m doorway: A's tiles T1 and T2 along its side, B beyond (Dir from A to B).</summary>
    public sealed class Doorway
    {
        /// <summary>The space it opens from, and the one beyond.</summary>
        public Space A = null!, B = null!;
        /// <summary>A's two tiles along the side it opens on.</summary>
        public (int I, int J) T1, T2;
        /// <summary>The way from A to B.</summary>
        public (int X, int Z) Dir;
        /// <summary>What stands in it.</summary>
        public Door Kind;
        /// <summary>Made to close a loop rather than to reach a new space.</summary>
        public bool Loop;

        /// <summary>The space on the other side from <paramref name="s"/>.</summary>
        public Space Other(Space s) => s == A ? B : A;

        /// <summary>The middle of the doorway (metres), on the wall line.</summary>
        public (float X, float Z) Center => (T1.I + T2.I + 1 + Dir.X, T1.J + T2.J + 1 + Dir.Z);
    }

    /// <summary>One floor of the dungeon.</summary>
    public sealed class Level
    {
        /// <summary>0 the first, counting down.</summary>
        public int Index;
        /// <summary>The space each taken tile belongs to.</summary>
        public readonly Dictionary<(int, int), Space> Owner = [];
        /// <summary>Cells with no floor: a stairwell.</summary>
        public readonly HashSet<(int, int)> Holes = [];
        /// <summary>Cells with no roof: under a stairwell.</summary>
        public readonly HashSet<(int, int)> Open = [];
        /// <summary>Cells a tall room rises through: nothing on the level above may stand in them.</summary>
        public readonly HashSet<(int, int)> TallCells = [];
        /// <summary>The level's rooms and corridors, in the order made.</summary>
        public readonly List<Space> Spaces = [];
    }

    /// <summary>The levels, first (top) to last.</summary>
    public readonly List<Level> Levels = [];
    /// <summary>Every space of every level, in the order made.</summary>
    public readonly List<Space> Spaces = [];
    /// <summary>Every doorway of every level.</summary>
    public readonly List<Doorway> Doors = [];
    /// <summary>What could not be made, for the user.</summary>
    public readonly List<string> Notes = [];
    /// <summary>The entrance hall on the first level, at the origin.</summary>
    public Space Entrance = null!;
    /// <summary>The boss's arena and the treasury beyond it (null when there was no room for them).</summary>
    public Space? Boss, Treasury;

    /// <summary>The 8 m cell (4 × 4 tiles) a tile is in.</summary>
    public static (int, int) CellOf((int I, int J) t) => (FloorDiv(t.I, 4), FloorDiv(t.J, 4));

    /// <summary>Integer division rounding down, also for negatives.</summary>
    public static int FloorDiv(int a, int b) => (int)MathF.Floor(a / (float)b);

    /// <summary>The height of a level's floor (metres): 6 m below the one above.</summary>
    public static float FloorY(int level) => -6f * level;

    /// <summary>How high a space's walls go above its floor (metres; 0 for none).</summary>
    public static float HeightOf(Space? s) => s == null ? 0 : s.Tall ? 10 : 4;

    /// <summary>The space that owns a tile on a level, if any.</summary>
    public Space? At(int level, (int, int) tile) => Levels[level].Owner.GetValueOrDefault(tile);
}

/// <summary>
/// Lays out a <see cref="DungeonPlan"/> from the settings and a style: on each level the way through, stairs
/// down or the antechamber, arena and treasury, rooms off the way (some twinned), then loops; then the
/// boss's key and hidden caches, and what stands in each doorway.
/// </summary>
internal sealed class DungeonPlanner
{
    /// <summary>The four ways along the grid: +z, +x, -z, -x.</summary>
    private static readonly (int X, int Z)[] Dirs = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    /// <summary>The plan being made.</summary>
    private readonly DungeonPlan _plan = new();
    private readonly DungeonGen.Settings _s;
    private readonly Style _style;
    private readonly Random _rnd;
    /// <summary>Tiles from the origin a level may reach.</summary>
    private readonly int _extent;
    /// <summary>The kinds of room made so far, so unique ones come once.</summary>
    private readonly HashSet<Room> _used = [];
    /// <summary>The next space's <c>Id</c>.</summary>
    private int _next;

    /// <summary>Sets up a planner; the size of the settings decides how far a level may reach.</summary>
    private DungeonPlanner(DungeonGen.Settings s, Style style, Random rnd)
    {
        _s = s;
        _style = style;
        _rnd = rnd;
        _extent = 14 + 6 * Math.Clamp(s.Size, 1, 4);
    }

    /// <summary>Lays out a dungeon; everything random comes from <paramref name="rnd"/>.</summary>
    public static DungeonPlan Make(DungeonGen.Settings s, Style style, Random rnd)
    {
        var p = new DungeonPlanner(s, style, rnd);
        p.Build();
        return p._plan;
    }

    /// <summary>The way to the right of one facing <paramref name="d"/>.</summary>
    private static (int X, int Z) Right((int X, int Z) d) => (d.Z, -d.X);

    /// <summary>The tile <paramref name="n"/> steps from <paramref name="t"/> along <paramref name="d"/>.</summary>
    private static (int, int) Add((int I, int J) t, (int X, int Z) d, int n = 1) => (t.I + d.X * n, t.J + d.Z * n);

    /// <summary>A random number from min to max in steps of 2 (min, min + 2, ...).</summary>
    private int Even(int min, int max) => min + 2 * _rnd.Next((max - min) / 2 + 1);

    /// <summary>One of the items at random, each as likely as its weight.</summary>
    private T Pick<T>(IReadOnlyList<(T Item, float Weight)> items)
    {
        float total = items.Sum(i => i.Weight), roll = (float)_rnd.NextDouble() * total;
        foreach (var (item, w) in items)
        {
            if ((roll -= w) <= 0)
            {
                return item;
            }
        }
        return items[^1].Item;
    }

    /// <summary>Makes the whole plan into <see cref="_plan"/>.</summary>
    private void Build()
    {
        int levels = Math.Clamp(_s.Levels, 1, DungeonGen.MaxLevels);
        int perLevel = new[] { 5, 8, 12, 17 }[Math.Clamp(_s.Size, 1, 4) - 1];
        for (int k = 0; k < levels; k++)
        {
            _plan.Levels.Add(new DungeonPlan.Level { Index = k });
        }
        DungeonPlan.Space? start = null;
        for (int k = 0; k < levels; k++)
        {
            bool last = k == levels - 1;
            var l = _plan.Levels[k];
            if (k == 0)
            {
                var spec = SpecOf(Room.Entrance);
                int w = Even(spec.WMin, spec.WMax), d = Even(spec.DMin, spec.DMax);
                start = NewSpace(l, Room.Entrance, (0, 1), Rect(-w / 2, 0, w, d));
                start.Main = true;
                _plan.Entrance = start;
            }
            // The way through: the style's main rooms in turn, then down, or to the boss.
            int mainCount = Math.Max(2, (int)MathF.Round(perLevel * 0.4f));
            var cur = start!;
            for (int i = 0; i < mainCount; i++)
            {
                var room = _style.Main[(i + k) % _style.Main.Length];
                if (SpecOf(room).Unique && _used.Contains(room))
                {
                    room = Room.Hub;
                }
                var n = Attach(l, cur, room, main: true) ?? Attach(l, AnyParent(l, main: true), room, main: true);
                if (n != null)
                {
                    n.Main = true;
                    cur = n;
                }
            }
            if (!last)
            {
                var top = AttachStairs(l, cur) ?? l.Spaces.Where(s => s.IsRoom && CanParent(s)).OrderByDescending(s => Dist2(s, _plan.Entrance)).Select(s => AttachStairs(l, s)).FirstOrDefault(s => s != null);
                if (top == null)
                {
                    _plan.Notes.Add($"no room for stairs down from level {k + 1}; the levels below are left out");
                    levels = k + 1;
                    _plan.Levels.RemoveRange(levels, _plan.Levels.Count - levels);
                    last = true;
                }
                else
                {
                    start = _plan.Levels[k + 1].Spaces[0];
                }
            }
            if (last)
            {
                // The antechamber is kept only with the arena beyond it: one with no arena would be a dead end.
                var before = Mark(l);
                var ante = Attach(l, cur, Room.Antechamber, main: true) ?? Attach(l, AnyParent(l, main: true), Room.Antechamber, main: true);
                var boss = ante == null ? null : Attach(l, ante, Room.Boss, main: true, forward: true) ?? Attach(l, ante, Room.Boss, main: true);
                if (boss == null)
                {
                    Drop(l, before);
                    // Not enough room ahead: anywhere it fits.
                    foreach (var p in l.Spaces.Where(s => s.IsRoom && CanParent(s)).OrderByDescending(s => Dist2(s, _plan.Entrance)).ToList())
                    {
                        ante = Attach(l, p, Room.Antechamber, main: true);
                        boss = ante == null ? null : Attach(l, ante, Room.Boss, main: true);
                        if (boss != null)
                        {
                            break;
                        }
                        Drop(l, before);
                    }
                }
                if (boss != null)
                {
                    _plan.Boss = boss;
                    _plan.Treasury = Attach(l, boss, Room.Treasury, main: true, forward: true) ?? Attach(l, boss, Room.Treasury, main: true);
                }
                else
                {
                    _plan.Notes.Add("no room for the boss arena");
                }
            }
            // Rooms off the way: wings, some with a twin across, some leading further.
            int sides = perLevel - mainCount;
            DungeonPlan.Space? wing = null;
            for (int i = 0, tries = 0; i < sides && tries < sides * 6; tries++)
            {
                // Wings: a side room often grows off the last one, and what it is leans to what that one is (a kitchen
                // by the feast hall, cells by the guard room, an ossuary by the burial chamber: rooms that belong together).
                var parent = wing != null && CanParent(wing) && _rnd.NextDouble() < 0.55 ? wing : SideParent(l);
                if (parent == null)
                {
                    break;
                }
                var room = Pick([.. _style.Side.Select(s => (s.Room, s.Weight * (Kin(parent.Room).Contains(s.Room) ? 5f : 1f)))]);
                if (SpecOf(room).Unique && _used.Contains(room))
                {
                    continue;
                }
                var n = Attach(l, parent, room, main: false);
                if (n == null)
                {
                    continue;
                }
                wing = n;
                i++;
                if (_rnd.NextDouble() < _style.Symmetry && Twin(l, n) != null)
                {
                    i++;
                }
            }
            AddLoops(l);
            AddStubs(l);
        }
        // The boss's key, in a chest far from the boss's door; a hidden cache or two.
        if (_s.BossKey && _plan.Boss != null)
        {
            var dist = Distances(_plan.Boss);
            var keyRoom = _plan.Spaces.Where(s => s.IsRoom && !s.Main && s.Room is not (Room.Secret or Room.StairTop or Room.StairBottom or Room.Treasury) && dist.ContainsKey(s))
                .OrderByDescending(s => dist[s] + _rnd.NextDouble()).FirstOrDefault()
                ?? _plan.Spaces.Where(s => s.IsRoom && s.Room is not (Room.Boss or Room.Treasury or Room.Antechamber or Room.StairTop or Room.StairBottom)).OrderByDescending(s => dist.GetValueOrDefault(s)).FirstOrDefault();
            keyRoom?.KeyChest = true;
        }
        if (_s.Secrets)
        {
            int want = 1 + (_plan.Spaces.Count(s => s.IsRoom) > 20 ? 1 : 0);
            for (int t = 0, made = 0; made < want && t < 30; t++)
            {
                var l = _plan.Levels[_rnd.Next(_plan.Levels.Count)];
                var parent = l.Spaces.Where(s => s.IsRoom && CanParent(s) && !s.Main).OrderBy(_ => _rnd.Next()).FirstOrDefault();
                if (parent != null && Attach(l, parent, Room.Secret, main: false, door: Door.Secret) != null)
                {
                    made++;
                }
            }
        }
        AssignDoors();
    }

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

    /// <summary>A space to put a side room off, weighted towards crossings and long halls; null when none can lead on.</summary>
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
        // Not in a corner, not on another doorway's tiles or right next to them.
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

    /// <summary>What a placement would make: the room's tiles, the corridor's, where they meet.</summary>
    /// <param name="Box">The room's box (tiles).</param>
    /// <param name="Corridor">The corridor's tiles (none: wall to wall).</param>
    /// <param name="T1">The parent's first tile of the doorway.</param>
    /// <param name="T2">The parent's second tile of the doorway.</param>
    /// <param name="Dir">The way from the parent into the room.</param>
    /// <param name="Shift">Tiles the room is shifted across from centred.</param>
    /// <param name="Length">The corridor's length (tiles).</param>
    /// <param name="Tall">The room rises through two floors.</param>
    /// <param name="Shape">The room's shape within its box.</param>
    /// <param name="Tiles">The room's tiles (its box, less what the shape cuts away).</param>
    /// <param name="E1">The corridor's first tile at its far end, where the room's doorway is (T1 when there is no corridor).</param>
    /// <param name="E2">Its second.</param>
    /// <param name="EndDir">The way into the room from there (Dir unless the corridor bends).</param>
    private sealed record Placement((int X0, int Z0, int W, int H) Box, List<(int, int)> Corridor, (int, int) T1, (int, int) T2, (int X, int Z) Dir, int Shift, int Length, bool Tall,
        DungeonPlan.Shape Shape, List<(int, int)> Tiles, (int, int) E1, (int, int) E2, (int X, int Z) EndDir);

    /// <summary>
    /// The tiles of a room of this shape in a box entered along dir: the whole box less the corners the shape
    /// cuts (n tiles square: 1, or 2 in a big room), never the two middle tiles at the way in.
    /// </summary>
    /// <param name="box">The room's box.</param>
    /// <param name="dir">The way in.</param>
    /// <param name="shape">Its shape.</param>
    public static List<(int, int)> ShapeTiles((int X0, int Z0, int W, int H) box, (int X, int Z) dir, DungeonPlan.Shape shape)
    {
        var probe = new DungeonPlan.Space { X0 = box.X0, Z0 = box.Z0, W = box.W, H = box.H, Dir = dir };
        int across = probe.Across, deep = probe.Deep;
        int n = across >= 10 && deep >= 10 ? 2 : 1;
        bool Cut(int a, int b) => shape switch
        {
            DungeonPlan.Shape.Cross => (a < n || a >= across - n) && (b < n || b >= deep - n),
            DungeonPlan.Shape.Apse => (a < n || a >= across - n) && b >= deep - n,
            DungeonPlan.Shape.L => a < across / 2 - 1 && b < deep / 2,
            DungeonPlan.Shape.T => (a < across / 2 - 1 || a > across / 2) && b < deep / 2,
            _ => false,
        };
        var tiles = new List<(int, int)>();
        for (int a = 0; a < across; a++)
        {
            for (int b = 0; b < deep; b++)
            {
                if (!Cut(a, b))
                {
                    tiles.Add(probe.TileAt(a, b));
                }
            }
        }
        return tiles;
    }

    /// <summary>
    /// A shape for a room of this kind and size, by chance: crossings, shrines, arenas and tombs are often
    /// notched at the corners, chapels, halls and throne rooms end in an apse, store rooms and quarters lose a
    /// corner or neck in at the door; long rooms, cell blocks, stairs and the entrance stay whole.
    /// </summary>
    private DungeonPlan.Shape ShapeFor(Room room, int across, int deep)
    {
        if (across < 6 || deep < 6)
        {
            return DungeonPlan.Shape.Rect;
        }
        double roll = _rnd.NextDouble();
        return room switch
        {
            Room.Hub or Room.Shrine or Room.Totem => roll < 0.45 ? DungeonPlan.Shape.Cross : DungeonPlan.Shape.Rect,
            Room.Boss => roll < 0.6 ? DungeonPlan.Shape.Cross : DungeonPlan.Shape.Rect,
            Room.Tomb or Room.Treasury => roll < 0.35 ? DungeonPlan.Shape.Cross : DungeonPlan.Shape.Rect,
            Room.Chapel => roll < 0.45 ? DungeonPlan.Shape.Apse : roll < 0.6 ? DungeonPlan.Shape.Cross : DungeonPlan.Shape.Rect,
            Room.Hall or Room.Throne => roll < 0.35 ? DungeonPlan.Shape.Apse : DungeonPlan.Shape.Rect,
            Room.Burial or Room.Ossuary => roll < 0.3 ? DungeonPlan.Shape.L : DungeonPlan.Shape.Rect,
            Room.Store or Room.Quarters or Room.Workshop or Room.Guard or Room.Library or Room.Kitchen or Room.Armory or Room.Barracks or Room.Den or Room.Torture =>
                roll < 0.3 ? DungeonPlan.Shape.L : roll < 0.42 && across >= 8 ? DungeonPlan.Shape.T : DungeonPlan.Shape.Rect,
            _ => DungeonPlan.Shape.Rect,
        };
    }

    /// <summary>
    /// Tries to place a room of w × d tiles (across, deep) off side dir of parent, at slot t1/t2, with a
    /// corridor of len tiles and the room shifted across by shift tiles from centred. Aligned: the box must
    /// cover whole cells; tall: nothing may be in its cells on the level above.
    /// </summary>
    /// <returns>Null when it does not fit.</returns>
    private Placement? TryPlace(DungeonPlan.Level l, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir, int w, int d, int len, int shift,
        bool aligned, bool tall, DungeonPlan.Shape shape = DungeonPlan.Shape.Rect)
    {
        var (x, z) = (t2.I - t1.I, t2.J - t1.J); // across, t1 to t2
        var corridor = new List<(int, int)>();
        for (int t = 1; t <= len; t++)
        {
            corridor.Add(Add(t1, dir, t));
            corridor.Add(Add(t2, dir, t));
        }
        if (corridor.Any(c => !Free(l, c)))
        {
            return null;
        }
        var row0 = Add(t1, dir, len + 1);
        int off = w / 2 - 1 + shift;
        var first = (row0.Item1 - x * off, row0.Item2 - z * off);
        var far = (first.Item1 + x * (w - 1) + dir.X * (d - 1), first.Item2 + z * (w - 1) + dir.Z * (d - 1));
        var box = (Math.Min(first.Item1, far.Item1), Math.Min(first.Item2, far.Item2), Math.Abs(far.Item1 - first.Item1) + 1, Math.Abs(far.Item2 - first.Item2) + 1);
        if (off < 0 || off > w - 2)
        {
            return null;
        }
        if (aligned && (box.Item1 % 4 != 0 || box.Item2 % 4 != 0 || box.Item3 % 4 != 0 || box.Item4 % 4 != 0))
        {
            return null;
        }
        foreach (var t in Box(box))
        {
            if (!Free(l, t))
            {
                return null;
            }
        }
        if (tall && l.Index > 0)
        {
            // Two floors high: nothing on the level above in its cells.
            var above = _plan.Levels[l.Index - 1];
            var cells = Box(box).Select(DungeonPlan.CellOf).ToHashSet();
            if (above.Owner.Keys.Any(t => cells.Contains(DungeonPlan.CellOf(t))))
            {
                return null;
            }
        }
        // A shaped room keeps its whole box clear (what it cuts away stays empty, walled off), its tiles being
        // what it floors; the way in must be whole.
        var tiles = shape == DungeonPlan.Shape.Rect ? [.. Box(box)] : ShapeTiles(box, dir, shape);
        if (shape != DungeonPlan.Shape.Rect && (!tiles.Contains(row0) || !tiles.Contains(Add(t2, dir, len + 1))))
        {
            return null;
        }
        return new Placement(box, corridor, t1, t2, dir, shift, len, tall, shape, tiles, Add(t1, dir, len), Add(t2, dir, len), dir);
    }

    /// <summary>
    /// Tries to place a room off parent's slot t1/t2 at the end of a corridor that bends: len tiles ahead, an elbow,
    /// then len2 tiles to the right (side 1) or the left (-1), the room entered that way, wall to wall.
    /// </summary>
    /// <returns>Null when it does not fit.</returns>
    private Placement? TryPlaceBent(DungeonPlan.Level l, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir, int w, int d, int len, int side, int len2, int shift,
        bool aligned, bool tall, DungeonPlan.Shape shape)
    {
        var across = (X: t2.I - t1.I, Z: t2.J - t1.J);
        var turn = side > 0 ? across : (X: -across.X, Z: -across.Z);
        var corridor = new List<(int, int)>();
        for (int k = 1; k <= len; k++)
        {
            corridor.Add(Add(t1, dir, k));
            corridor.Add(Add(t2, dir, k));
        }
        // The elbow: two rows over both columns; the leg runs on from the column on the turn's side.
        var (a1, a2, b1, b2) = (Add(t1, dir, len + 1), Add(t1, dir, len + 2), Add(t2, dir, len + 1), Add(t2, dir, len + 2));
        corridor.AddRange([a1, a2, b1, b2]);
        var (s1, s2) = side > 0 ? (b1, b2) : (a1, a2);
        for (int m = 1; m <= len2; m++)
        {
            corridor.Add(Add(s1, turn, m));
            corridor.Add(Add(s2, turn, m));
        }
        if (corridor.Any(c => !Free(l, c)))
        {
            return null;
        }
        var (e1, e2) = (Add(s1, turn, len2), Add(s2, turn, len2));
        var p = TryPlace(l, e1, e2, turn, w, d, 0, shift, aligned, tall, shape);
        if (p == null || Box(p.Box).Any(corridor.Contains))
        {
            return null;
        }
        return p with { Corridor = corridor, T1 = t1, T2 = t2, Dir = dir, Length = len + 2 + len2, E1 = e1, E2 = e2, EndDir = turn };
    }

    /// <summary>How often a corridor to a room bends on the way, for the style: often in catacombs, warrens and holds, seldom in a temple.</summary>
    private float BendChance => _style.Name switch
    {
        "Catacombs" or "Goblin warren" or "Dvergr hold" or "Ruins" => 0.4f,
        "Temple" => 0.1f,
        _ => 0.25f,
    };

    /// <summary>
    /// Places a room of a kind off parent, trying many sides, slots, sizes and corridors and keeping the best
    /// (near the dungeon's middle, centred, straight ahead on the main way, tall when wanted, short corridor).
    /// </summary>
    /// <param name="l">The level.</param>
    /// <param name="parent">The space it opens off.</param>
    /// <param name="room">The kind of room.</param>
    /// <param name="main">On the main way: straight ahead is favoured.</param>
    /// <param name="forward">Only straight ahead (the parent's way in).</param>
    /// <param name="door">What stands in the doorway (null: decided later); Secret hides the way from the parent.</param>
    /// <returns>The new room, or null when it fits nowhere or parent cannot lead on.</returns>
    private DungeonPlan.Space? Attach(DungeonPlan.Level l, DungeonPlan.Space parent, Room room, bool main, bool forward = false, Door? door = null)
    {
        if (!CanParent(parent, room))
        {
            return null;
        }
        var spec = SpecOf(room);
        bool tallWanted = spec.Tall && _rnd.NextDouble() < _style.TallChance;
        Placement? best = null;
        float bestScore = float.MinValue;
        for (int attempt = 0; attempt < 48; attempt++)
        {
            bool tall = tallWanted && attempt < 32;
            var dirs = Dirs.Where(dd => dd != (-parent.Dir.X, -parent.Dir.Z) || parent.Room == Room.Hub).ToList();
            var dir = forward ? parent.Dir : Pick(dirs.Select(dd => (dd, dd == parent.Dir ? (main ? 5f : 0.7f) : (main ? 1f : 3f))).ToList());
            var side = SideTiles(parent, dir);
            if (side.Count < 2)
            {
                continue;
            }
            // The slot: in the middle of the side most of the time.
            int mid = side.Count / 2 - 1, at = _rnd.NextDouble() < 0.7 ? mid : _rnd.Next(side.Count - 1);
            var (t1, t2) = (side[at], side[at + 1]);
            if (Math.Abs(t1.Item1 - t2.Item1) + Math.Abs(t1.Item2 - t2.Item2) != 1 || !SlotAllowed(parent, t1, t2, dir))
            {
                continue;
            }
            int w = Even(spec.WMin, spec.WMax), d = Even(spec.DMin, spec.DMax);
            // Now and then a room is entered on its long side.
            if (spec.WMax != spec.WMin && spec.DMax > spec.WMax && room is not (Room.Gallery or Room.Mine or Room.Cells or Room.Feast or Room.Entrance) && _rnd.NextDouble() < 0.25)
            {
                (w, d) = (d, w);
            }
            if (tall)
            {
                w = Math.Max(8, (w + 3) / 4 * 4);
                d = Math.Max(8, (d + 3) / 4 * 4);
            }
            var shape = ShapeFor(room, w, d);
            bool wall = _rnd.NextDouble() < 0.25 && _style.CorridorMin <= 1 && parent.Room != Room.StairBottom;
            int len = wall ? 0 : _rnd.Next(_style.CorridorMin, _style.CorridorMax + 1);
            int shift = _rnd.NextDouble() < 0.75 || room == Room.Cells ? 0 : _rnd.Next(-(w / 2 - 1), w / 2);
            bool bend = len > 0 && !tall && room != Room.Cells && _rnd.NextDouble() < BendChance * (main ? 0.5f : 1f);
            Placement? p = null;
            if (bend)
            {
                p = TryPlaceBent(l, t1, t2, dir, w, d, Math.Max(1, len), _rnd.Next(2) * 2 - 1, _rnd.Next(2, 6), shift, aligned: false, tall: false, shape);
            }
            else if (tall)
            {
                // Aligned to cells: try nearby shifts and lengths.
                for (int dl = 0; dl < 4 && p == null; dl++)
                {
                    for (int sh = -(w / 2 - 1); sh <= w / 2 - 1 && p == null; sh++)
                    {
                        p = TryPlace(l, t1, t2, dir, w, d, len + dl, sh, aligned: true, tall: true, shape);
                    }
                }
            }
            else
            {
                p = TryPlace(l, t1, t2, dir, w, d, len, shift, aligned: false, tall: false, shape);
            }
            if (p == null)
            {
                continue;
            }
            float cx = (p.Box.X0 + p.Box.W / 2f) * 2, cz = (p.Box.Z0 + p.Box.H / 2f) * 2;
            float score = (float)_rnd.NextDouble() * 2 - 0.004f * (cx * cx + (cz - 40) * (cz - 40)) / 10 + (p.Shift == 0 ? 1 : 0) + (dir == parent.Dir && main ? 2 : 0)
                + (tall ? 3 : 0) - p.Length * 0.15f;
            if (score > bestScore)
            {
                best = p;
                bestScore = score;
            }
        }
        if (best == null)
        {
            return null;
        }
        var n = NewSpace(l, room, best.EndDir, best.Box, best.Shape == DungeonPlan.Shape.Rect ? null : best.Tiles);
        n.Shape = best.Shape;
        n.Parent = parent;
        n.Tall = best.Tall;
        if (n.Tall)
        {
            foreach (var t in n.Tiles)
            {
                l.TallCells.Add(DungeonPlan.CellOf(t));
            }
        }
        Join(l, parent, n, best, door);
        if (!n.Tall && room is Room.Gallery or Room.Burial or Room.Ossuary or Room.Chapel or Room.Hall or Room.Tomb or Room.Throne or Room.Armory or Room.Library && _rnd.NextDouble() < 0.4)
        {
            Recesses(l, n, 2, _rnd.Next(1, 3));
        }
        return n;
    }

    /// <summary>
    /// Adds up to count recesses to a space: a run of width tiles pushed out of one of its walls, where the tiles
    /// beyond are free (an alcove off a room, a niche off a corridor). The space's tiles are then more than its box
    /// says, so it is marked shaped; its box grows to hold them.
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
    /// not a centred, ordinary side room or the mirror does not fit.
    /// </summary>
    private DungeonPlan.Space? Twin(DungeonPlan.Level l, DungeonPlan.Space n)
    {
        var parent = n.Parent;
        if (parent == null || n.Shift != 0 || n.Tall || n.Dir == parent.Dir || n.Dir == (-parent.Dir.X, -parent.Dir.Z))
        {
            return null;
        }
        var first = parent.Doors.FirstOrDefault(d => d.A == parent && (d.B == n || d.B.Corridor && d.B.Doors.Any(x => x.B == n)));
        if (first == null || first.B.Corridor && first.B.Shape != DungeonPlan.Shape.Rect)
        {
            return null;
        }
        (int, int) Mirror((int I, int J) t) => n.Dir.X != 0 ? (2 * parent.X0 + parent.W - 1 - t.I, t.J) : (t.I, 2 * parent.Z0 + parent.H - 1 - t.J);
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
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var dir = attempt < 12 ? parent.Dir : Dirs[_rnd.Next(4)];
            if (dir == (-parent.Dir.X, -parent.Dir.Z) && parent.Room != Room.Hub)
            {
                continue;
            }
            var side = SideTiles(parent, dir);
            for (int at = 0; at + 1 < side.Count; at++)
            {
                var (t1, t2) = (side[at], side[at + 1]);
                if (Math.Abs(t1.Item1 - t2.Item1) + Math.Abs(t1.Item2 - t2.Item2) != 1 || !SlotAllowed(parent, t1, t2, dir))
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
    /// How many rooms away each space is from <paramref name="from"/>, through doorways and stairs
    /// (corridors do not count); spaces not reached are missing.
    /// </summary>
    private Dictionary<DungeonPlan.Space, int> Distances(DungeonPlan.Space from)
    {
        var dist = new Dictionary<DungeonPlan.Space, int> { [from] = 0 };
        var queue = new Queue<DungeonPlan.Space>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            var next = s.Doors.Select(d => d.Other(s)).ToList();
            // The stairs join a stairwell's two rooms.
            next.AddRange(_plan.Spaces.Where(o => o.Parent == s && o.Room == Room.StairBottom || s.Room == Room.StairBottom && s.Parent == o));
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

    /// <summary>
    /// Joins rooms that are far apart on the way but close in space: through a shared wall, or by a
    /// short straight corridor. How many follows the settings' Loops and the level's rooms.
    /// </summary>
    private void AddLoops(DungeonPlan.Level l)
    {
        int want = (int)MathF.Round(_s.Loops * l.Spaces.Count(s => s.IsRoom) / 5f);
        for (int made = 0, tries = 0; made < want && tries < want * 3 + 3; tries++)
        {
            var options = new List<(float Score, Action Make)>();
            foreach (var a in l.Spaces.Where(Loopable))
            {
                var dist = Distances(a);
                foreach (var dir in Dirs)
                {
                    var side = SideTiles(a, dir);
                    for (int at = 0; at + 1 < side.Count; at++)
                    {
                        var (t1, t2) = (side[at], side[at + 1]);
                        if (Math.Abs(t1.Item1 - t2.Item1) + Math.Abs(t1.Item2 - t2.Item2) != 1 || !SlotAllowed(a, t1, t2, dir))
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
                                int d = dist.GetValueOrDefault(b, 99);
                                var path = Enumerable.Range(1, len).SelectMany(t => new[] { Add(t1, dir, t), Add(t2, dir, t) }).ToList();
                                if (d >= 3 && path.All(t => Free(l, t)) && SlotAllowed(b, n1, n2, (-dir.X, -dir.Z)))
                                {
                                    var (aa, bb, tt1, tt2, dd, ll, pp) = (a, b, t1, t2, dir, len, path);
                                    void Join()
                                    {
                                        if (ll == 0)
                                        {
                                            Connect(aa, bb, tt1, tt2, dd, loop: true);
                                        }
                                        else
                                        {
                                            var c = NewCorridor(l, pp, dd);
                                            Connect(aa, c, tt1, tt2, dd, loop: true);
                                            Connect(c, bb, Add(tt1, dd, ll), Add(tt2, dd, ll), dd, loop: true);
                                        }
                                    }
                                    options.Add((d - len * 0.3f + (float)_rnd.NextDouble(), Join));
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
    /// Dead ends: a corridor or two off a side room that leads nowhere but a cache at its far end (the chest
    /// corridors of Minecraft's strongholds, the blind passages of old crypts). Often in crypts, catacombs,
    /// prisons and ruins, less in the rest. It touches no other corridor (two corridors open into each other).
    /// </summary>
    private void AddStubs(DungeonPlan.Level l)
    {
        float chance = _style.Name is "Catacombs" or "Crypt" or "Prison" or "Ruins" ? 0.7f : 0.35f;
        int want = _rnd.NextDouble() < chance ? 1 + (l.Spaces.Count(s => s.IsRoom) > 10 ? 1 : 0) : 0;
        for (int made = 0, tries = 0; made < want && tries < 24; tries++)
        {
            var a = l.Spaces.Where(s => Loopable(s) && s.Room is not (Room.Entrance or Room.Antechamber)).OrderBy(_ => _rnd.Next()).FirstOrDefault();
            if (a == null)
            {
                return;
            }
            var dir = Dirs[_rnd.Next(Dirs.Length)];
            var side = SideTiles(a, dir);
            if (side.Count < 2)
            {
                continue;
            }
            int at = _rnd.Next(side.Count - 1);
            var (t1, t2) = (side[at], side[at + 1]);
            if (Math.Abs(t1.Item1 - t2.Item1) + Math.Abs(t1.Item2 - t2.Item2) != 1 || !SlotAllowed(a, t1, t2, dir))
            {
                continue;
            }
            // Not beside another doorway: two openings side by side leave the wall between them nothing to hold to.
            if (a.Doors.Any(d => new[] { t1, t2 }.Any(t => Math.Abs(t.Item1 - d.T1.I) + Math.Abs(t.Item2 - d.T1.J) <= 2 || Math.Abs(t.Item1 - d.T2.I) + Math.Abs(t.Item2 - d.T2.J) <= 2)))
            {
                continue;
            }
            int len = _rnd.Next(2, 5);
            var path = Enumerable.Range(1, len).SelectMany(k => new[] { Add(t1, dir, k), Add(t2, dir, k) }).ToList();
            var beyond = path.SelectMany(t => Dirs.Select(d => Add(t, d))).Where(t => !path.Contains(t) && t != t1 && t != t2).ToList();
            if (path.Any(t => !Free(l, t)) || beyond.Any(t => l.Owner.GetValueOrDefault(t) is { } o && (o.Corridor || o != a)))
            {
                continue;
            }
            var c = NewCorridor(l, path, dir);
            c.Parent = a;
            c.DeadEnd = true;
            Connect(a, c, t1, t2, dir);
            made++;
        }
    }

    /// <summary>
    /// Decides what stands in each doorway: the key gate (or a grand one) before the arena, a gate to the
    /// treasury and on the way between levels, the style's doors elsewhere.
    /// </summary>
    private void AssignDoors()
    {
        foreach (var d in _plan.Doors)
        {
            var rooms = new[] { d.A, d.B };
            if (d.Kind == Door.Secret)
            {
                continue;
            }
            if (rooms.Any(r => r.Room == Room.Boss) && rooms.Any(r => r.Room == Room.Antechamber || r.Corridor && r.Parent?.Room == Room.Antechamber))
            {
                d.Kind = _s.BossKey ? Door.Key : Door.Grand;
                continue;
            }
            if (rooms.Any(r => r.Room == Room.Treasury))
            {
                d.Kind = Door.Gate;
                continue;
            }
            if (rooms.Any(r => r.Room == Room.Secret))
            {
                d.Kind = Door.Open;
                continue;
            }
            if (rooms.Any(r => r.Room == Room.Cells) && _rnd.NextDouble() < 0.6)
            {
                d.Kind = Door.Gate;
                continue;
            }
            bool grand = rooms.Any(r => r.Room is Room.Hall or Room.Throne or Room.Feast or Room.Chapel) && rooms.All(r => !r.Corridor || true);
            var kind = Pick(_style.Doors);
            if (grand && _rnd.NextDouble() < 0.35)
            {
                kind = Door.Grand;
            }
            if (kind == Door.Grand && !rooms.Any(r => r.Room is Room.Hall or Room.Throne or Room.Feast or Room.Chapel or Room.Hub or Room.Boss))
            {
                kind = Door.Wood;
            }
            d.Kind = kind;
        }
        // Doors on the way between levels, so what lives below stays below.
        foreach (var top in _plan.Spaces.Where(s => s.Room == Room.StairTop))
        {
            foreach (var d in top.Doors.Where(d => d.Kind is Door.Open or Door.Curtain))
            {
                d.Kind = Door.Gate;
            }
        }
    }
}

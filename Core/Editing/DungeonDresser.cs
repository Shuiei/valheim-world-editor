using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

/// <summary>
/// What makes a generated dungeon's rooms what they are: their furniture and set pieces (colonnades,
/// thrones on a dais, pews before an altar, sarcophagi, cells behind bars...), their lights, what hangs
/// on their walls and lies on their floors, their decay, who guards them and what they hold. Each room
/// is dressed in its own frame: u across (right of the way in), v deep (0 its middle, the way in behind).
/// </summary>
internal sealed partial class DungeonDresser
{
    /// <summary>
    /// What stands on a floor goes 5 cm up: a piece whose middle of mass is right on a floor's surface
    /// gets no support from it in game.
    /// </summary>
    private const float Lift = 0.05f;

    private readonly Material _mat;
    /// <summary>The biome's palette (lights, foes, loot, clutter).</summary>
    private readonly Biome _b;
    private readonly Style _style;
    private readonly DungeonGen.Settings _s;
    private readonly Random _rnd;
    /// <summary>Where the dungeon's objects go.</summary>
    private readonly DungeonOut _o;
    /// <summary>How ruined (0 kept to 1): the settings' Decay, else the style's.</summary>
    private readonly float _decay;
    /// <summary>The foes to use: those picked in the settings, else the biome's.</summary>
    private readonly Foe[] _foes;
    /// <summary>The arena's boss: the one picked, the biome's when none is, null when "" is.</summary>
    private readonly Foe? _boss;
    /// <summary>The dungeon's name, for the entrance's sign.</summary>
    private readonly string _name;
    /// <summary>The lights the room being furnished asked for: 0 none, 1 the usual, 2 whatever the settings (important).</summary>
    private int _lights;
    /// <summary>Whether the room being furnished asked for its decay.</summary>
    private bool _decayed;

    /// <summary>Sets up a dresser for one dungeon.</summary>
    private DungeonDresser(Material mat, Biome b, Style style, DungeonGen.Settings s, Random rnd, DungeonOut o, string name)
    {
        _mat = mat;
        _b = b;
        _style = style;
        _s = s;
        _rnd = rnd;
        _o = o;
        _name = name;
        _decay = Math.Clamp(s.Decay ?? style.Decay, 0, 1);
        _foes = s.Foes is { Count: > 0 } chosen ? [.. chosen.Select(FoeOf)] : b.Foes;
        _boss = s.Boss == null ? b.Boss : s.Boss.Length == 0 ? null : FoeOf(s.Boss);
    }

    /// <summary>Furnishes, lights, guards and stocks every space of the plan.</summary>
    public static void Dress(DungeonPlan plan, Material mat, Biome b, Style style, DungeonGen.Settings s, Random rnd, DungeonOut o, string name)
    {
        var d = new DungeonDresser(mat, b, style, s, rnd, o, name);
        foreach (var sp in plan.Spaces)
        {
            d.Furnish(new Frame(sp, mat, plan.Levels[sp.Level]));
        }
    }

    /// <summary>
    /// A foe by its spawner's or its creature's name; one not in any biome is taken as it is (a Spawner_
    /// name for both, its creature the name without the prefix).
    /// </summary>
    public static Foe FoeOf(string name)
    {
        foreach (var f in Biomes.SelectMany(b => b.Foes.Concat(b.Elites).Append(b.Boss)))
        {
            if (f.Spawner == name || f.Creature == name)
            {
                return f;
            }
        }
        return name.StartsWith("Spawner_", StringComparison.Ordinal) ? new Foe(name, name["Spawner_".Length..]) : new Foe(name, name);
    }

    // ---- A room's frame.
    /// <summary>
    /// A space's own frame (u across, right of the way in; v deep, 0 its middle) and what is already taken
    /// in it.
    /// </summary>
    private sealed class Frame
    {
        /// <summary>The space.</summary>
        public readonly DungeonPlan.Space S;
        /// <summary>
        /// Its floor (the marble's top), the top of its floor finish, its wall height, its middle (world x, z)
        /// and the half width and half depth to the walls' inner faces (metres).
        /// </summary>
        public readonly float F, Top, H, Cx, Cz, HalfA, HalfD;
        /// <summary>The way in (deep, +v) and the way across (+u, to its right), in world axes.</summary>
        public readonly (int X, int Z) D, A;
        /// <summary>Circles kept free (world x, z, radius in metres).</summary>
        public readonly List<(float X, float Z, float R)> Taken = [];
        /// <summary>Its doorways: their middle (world x, z), the way into the room from them, the doorway.</summary>
        public readonly List<(float X, float Z, (int X, int Z) In, DungeonPlan.Doorway Door)> Mouths = [];

        /// <summary>The level the space is on: what lies beyond its walls.</summary>
        private readonly DungeonPlan.Level _level;

        /// <summary>The frame of a space, with the floor before each doorway kept free.</summary>
        public Frame(DungeonPlan.Space s, Material mat, DungeonPlan.Level level)
        {
            S = s;
            _level = level;
            F = DungeonPlan.FloorY(s.Level);
            Top = F + (s.IsRoom && s.Room is not (Room.StairTop or Room.StairBottom) ? s.Floor != null ? s.FloorTop : mat.Floor != null ? mat.FloorTop : 0 : 0);
            H = DungeonPlan.HeightOf(s);
            Cx = s.CenterX;
            Cz = s.CenterZ;
            D = s.Dir;
            A = (D.Z, -D.X);
            HalfA = s.Across - mat.Thick / 2;
            HalfD = s.Deep - mat.Thick / 2;
            foreach (var d in s.Doors)
            {
                var (x, z) = d.Center;
                var inward = d.A == s ? (-d.Dir.X, -d.Dir.Z) : d.Dir;
                Mouths.Add((x, z, inward, d));
                Taken.Add((x + inward.Item1 * 1.6f, z + inward.Item2 * 1.6f, 2.2f));
            }
        }

        /// <summary>Its floor area inside the walls (square metres): its tiles' less the walls' share.</summary>
        public float Area => S.Tiles.Count * 4f * (HalfA * HalfD) / (S.Across * S.Deep);

        /// <summary>Whether a point of the frame is over one of the room's own tiles (not what its shape cuts away).</summary>
        public bool Inside(float u, float v)
        {
            int a = (int)MathF.Floor((u + S.Across) / 2), b = (int)MathF.Floor((v + S.Deep) / 2);
            return a >= 0 && b >= 0 && a < S.Across && b < S.Deep && S.Has(S.TileAt(a, b));
        }

        /// <summary>A point of the frame (u, v; y as given) in world space.</summary>
        public Vector3 W(float u, float v, float y) => new(Cx + A.X * u + D.X * v, y, Cz + A.Z * u + D.Z * v);

        /// <summary>A world point (x, z) in the frame.</summary>
        public (float U, float V) Local(float x, float z) => ((x - Cx) * A.X + (z - Cz) * A.Z, (x - Cx) * D.X + (z - Cz) * D.Z);

        /// <summary>A turn in the frame (0 faces the far end) as a world turn (degrees).</summary>
        public float Yaw(float local) => DungeonOut.YawOf(D) + local;

        /// <summary>Whether a circle of radius r at (u, v) is inside the walls (over the room's own tiles) and clear of what is taken.</summary>
        public bool Free(float u, float v, float r)
        {
            if (Math.Abs(u) > HalfA - r + 0.15f || Math.Abs(v) > HalfD - r + 0.15f)
            {
                return false;
            }
            if (S.Shape != DungeonPlan.Shape.Rect)
            {
                float in_ = Math.Max(0, r - 0.15f);
                if (!Inside(u, v) || !Inside(u - in_, v) || !Inside(u + in_, v) || !Inside(u, v - in_) || !Inside(u, v + in_))
                {
                    return false;
                }
            }
            var p = W(u, v, 0);
            return Taken.All(t => (t.X - p.X) * (t.X - p.X) + (t.Z - p.Z) * (t.Z - p.Z) >= (t.R + r) * (t.R + r));
        }

        /// <summary>Keeps a circle of radius r at (u, v) for something.</summary>
        public void Take(float u, float v, float r)
        {
            var p = W(u, v, 0);
            Taken.Add((p.X, p.Z, r));
        }

        /// <summary>
        /// The room's corners on the walls' inner faces (u, v), with the way into the room from each (local yaw of the
        /// diagonal) and whether it is an outer corner of the room (convex) or one where a notch meets a wall.
        /// </summary>
        public List<(float U, float V, float Yaw, bool Convex)> Corners()
        {
            float thick = S.Across - HalfA;
            var corners = new List<(float, float, float, bool)>();
            bool In(int a, int b) => a >= 0 && b >= 0 && a < S.Across && b < S.Deep && S.Has(S.TileAt(a, b));
            for (int a = 0; a <= S.Across; a++)
            {
                for (int b = 0; b <= S.Deep; b++)
                {
                    // The point at the low corner of tile (a, b): the four tiles round it.
                    bool[] q = [In(a - 1, b - 1), In(a, b - 1), In(a - 1, b), In(a, b)];
                    int n = q.Count(x => x);
                    if (n is not (1 or 3))
                    {
                        continue;
                    }
                    // Toward the one tile inside, or away from the one missing.
                    int k = Array.IndexOf(q, n == 1);
                    float du = (k % 2 == 0 ? -1 : 1) * (n == 1 ? 1 : -1), dv = (k < 2 ? -1 : 1) * (n == 1 ? 1 : -1);
                    float u = 2 * a - S.Across + du * thick, v = 2 * b - S.Deep + dv * thick;
                    corners.Add((u, v, MathF.Atan2(du, dv) * 180 / MathF.PI, n == 1));
                }
            }
            return corners;
        }

        /// <summary>Whether a point on a wall is in front of a doorway (within clear metres of its middle).</summary>
        public bool NearDoor(float u, float v, float clear)
        {
            var p = W(u, v, 0);
            return Mouths.Any(m => (m.X - p.X) * (m.X - p.X) + (m.Z - p.Z) * (m.Z - p.Z) < clear * clear);
        }

        /// <summary>
        /// Whether there is a wall between the space's tile (a, b) and the tile beyond it in a direction (one of the
        /// space's own: none; a corridor beyond a corridor: none, they open into each other).
        /// </summary>
        private bool WallBeyond(int a, int b, int da, int db)
        {
            int na = a + da, nb = b + db;
            if (na >= 0 && nb >= 0 && na < S.Across && nb < S.Deep && S.Has(S.TileAt(na, nb)))
            {
                return false;
            }
            // The world tile beyond, through the frame's axes.
            var t = S.TileAt(a, b);
            var (x, z) = (da * A.X + db * D.X, da * A.Z + db * D.Z);
            var other = _level.Owner.GetValueOrDefault((t.Item1 + x, t.Item2 + z));
            return !(S.Corridor && other is { Corridor: true });
        }

        /// <summary>
        /// The room's nooks: its recesses (an alcove off a room, a niche off a corridor), each by its middle (u, v), the
        /// way out of it into the room (local yaw) and how many tiles wide it is; then any other tile walled on three
        /// sides, as a nook of one tile.
        /// </summary>
        public List<(float U, float V, float Yaw, int Width)> Nooks()
        {
            var nooks = new List<(float, float, float, int)>();
            var inRecess = new HashSet<(int, int)>();
            foreach (var (tiles, out_) in S.Recesses)
            {
                inRecess.UnionWith(tiles);
                // The tiles' middles (world: a tile (i, j) spans 2i..2i+2), taken into the frame; the way back is -out.
                var (u, v) = (0f, 0f);
                foreach (var (i, j) in tiles)
                {
                    var (tu, tv) = Local(2 * i + 1, 2 * j + 1);
                    (u, v) = (u + tu / tiles.Count, v + tv / tiles.Count);
                }
                float du = -(out_.X * A.X + out_.Z * A.Z), dv = -(out_.X * D.X + out_.Z * D.Z);
                nooks.Add((u, v, MathF.Atan2(du, dv) * 180 / MathF.PI, tiles.Count));
            }
            for (int a = 0; a < S.Across; a++)
            {
                for (int b = 0; b < S.Deep; b++)
                {
                    if (!S.Has(S.TileAt(a, b)) || inRecess.Contains(S.TileAt(a, b)))
                    {
                        continue;
                    }
                    var open = new List<(int, int)>();
                    foreach (var (da, db) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
                    {
                        if (!WallBeyond(a, b, da, db))
                        {
                            open.Add((da, db));
                        }
                    }
                    if (open.Count == 1)
                    {
                        nooks.Add((2 * a + 1 - S.Across, 2 * b + 1 - S.Deep, MathF.Atan2(open[0].Item1, open[0].Item2) * 180 / MathF.PI, 1));
                    }
                }
            }
            return nooks;
        }

        /// <summary>
        /// The room's walls as straight runs on their inner face: where each starts (u, v), which way it runs
        /// (du, dv, unit), how long it is (metres) and the way into the room from it (local yaw). The four walls of
        /// a whole box; more for a shaped room, round its notches; none where a corridor opens into another.
        /// </summary>
        public List<(float U, float V, float DU, float DV, float Length, float Yaw)> Walls()
        {
            float thick = S.Across - HalfA;
            // Each tile's walled sides, in the frame: (along the run, across the run) with the run's start.
            var segments = new List<(float Yaw, float Line, float At)>();
            for (int a = 0; a < S.Across; a++)
            {
                for (int b = 0; b < S.Deep; b++)
                {
                    if (!S.Has(S.TileAt(a, b)))
                    {
                        continue;
                    }
                    float u = 2 * a - S.Across, v = 2 * b - S.Deep; // the tile's low corner
                    if (WallBeyond(a, b, 0, -1))
                    {
                        segments.Add((0, v + thick, u));
                    }
                    if (WallBeyond(a, b, 0, 1))
                    {
                        segments.Add((180, v + 2 - thick, u));
                    }
                    if (WallBeyond(a, b, -1, 0))
                    {
                        segments.Add((90, u + thick, v));
                    }
                    if (WallBeyond(a, b, 1, 0))
                    {
                        segments.Add((-90, u + 2 - thick, v));
                    }
                }
            }
            var runs = new List<(float, float, float, float, float, float)>();
            foreach (var g in segments.GroupBy(s => (s.Yaw, s.Line)))
            {
                var ats = g.Select(s => s.At).Order().ToList();
                for (int i = 0; i < ats.Count;)
                {
                    int j = i;
                    while (j + 1 < ats.Count && MathF.Abs(ats[j + 1] - ats[j] - 2) < 0.01f)
                    {
                        j++;
                    }
                    float start = ats[i] + thick, length = ats[j] + 2 - thick - start;
                    bool alongU = g.Key.Yaw is 0 or 180;
                    runs.Add(alongU ? (start, g.Key.Line, 1, 0, length, g.Key.Yaw) : (g.Key.Line, start, 0, 1, length, g.Key.Yaw));
                    i = j + 1;
                }
            }
            return runs;
        }

        /// <summary>
        /// Spots along the walls (on their inner face), every step metres, facing in (local yaw), not by
        /// doorways.
        /// </summary>
        public List<(float U, float V, float Yaw)> WallSpots(float step, float clear = 3f)
        {
            var spots = new List<(float, float, float)>();
            foreach (var (u, v, du, dv, length, yaw) in Walls())
            {
                for (float t = step / 2; t <= length - step / 2 + 0.01f; t += step)
                {
                    spots.Add((u + du * t, v + dv * t, yaw));
                }
            }
            return [.. spots.Where(s => !NearDoor(s.Item1, s.Item2, clear))];
        }
    }

    /// <summary>Moves a wall spot in by depth metres.</summary>
    private static (float U, float V) In((float U, float V, float Yaw) s, float depth) =>
        (s.U + depth * MathF.Sin(s.Yaw * MathF.PI / 180), s.V + depth * MathF.Cos(s.Yaw * MathF.PI / 180));

    /// <summary>A random number from a to b.</summary>
    private float R(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

    /// <summary>One of the list at random.</summary>
    private T One<T>(T[] list) => list[_rnd.Next(list.Length)];

    /// <summary>One of the names at random.</summary>
    private string One(params string[] list) => list[_rnd.Next(list.Length)];

    /// <summary>One of the names at random, not the one just used when there is a choice (repeated detail is what the eye tires of first).</summary>
    private string Another(string[] list, string last)
    {
        int k = _rnd.Next(list.Length);
        return list.Length > 1 && list[k] == last ? list[(k + 1) % list.Length] : list[k];
    }

    /// <summary>True with chance p (0 to 1).</summary>
    private bool Chance(double p) => _rnd.NextDouble() < p;

    /// <summary>n as a whole count: its whole part, plus one with the chance of its fraction (0 for n &lt;= 0).</summary>
    private int Count(float n) => n <= 0 ? 0 : (int)MathF.Floor(n) + (_rnd.NextDouble() < n - MathF.Floor(n) ? 1 : 0);

    // ---- Placing.

    /// <summary>
    /// Places an object at (u, v) of a room, y above its floor (its foot, from the model), turned yaw
    /// (local; 0 faces the far end), keeping r metres around it for itself unless forced.
    /// </summary>
    /// <returns>False when the spot is not free (and it is not forced): nothing is placed.</returns>
    private bool Put(Frame f, string prefab, float u, float v, float yaw, float r, float y = 0, bool force = false,
        IReadOnlyList<(string, string, string)>? data = null, bool take = true)
    {
        if (!force && !f.Free(u, v, r) || force && !f.Inside(u, v))
        {
            return false;
        }
        if (r > 0 && take)
        {
            f.Take(u, v, r);
        }
        _o.Add(prefab, f.W(u, v, (OnMarble(prefab) ? f.F : f.Top) + Lift + y + DungeonProps.Foot(prefab)), f.Yaw(yaw + DungeonProps.Turn(prefab)), data);
        return true;
    }

    /// <summary>
    /// Whether a piece must stand on the marble itself, through any floor finish: a piece of stone, marble or grausten
    /// needs more support than a wooden floor or a marble tile gives (100 at the least; a floor passes on less).
    /// </summary>
    private static bool OnMarble(string prefab) => Stability.Known(prefab) && Stability.Shape(prefab).Material is 1 or 4 or 5;

    /// <summary>Tries a few spots near (u, v), up to spread metres off; false when none is free.</summary>
    private bool PutNear(Frame f, string prefab, float u, float v, float yaw, float r, float spread = 1.5f, float y = 0)
    {
        for (int i = 0; i < 8; i++)
        {
            if (Put(f, prefab, u + (i == 0 ? 0 : R(-spread, spread)), v + (i == 0 ? 0 : R(-spread, spread)), yaw, r, y))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Somewhere free in the room for a circle of radius r, margin metres from the walls; null after 30 tries. The
    /// tries are drawn from rnd when given (a stream of the room's own), else from the dungeon's.
    /// </summary>
    private (float U, float V)? Spot(Frame f, float r, float margin = 0.8f, Random? rnd = null)
    {
        rnd ??= _rnd;
        for (int i = 0; i < 30; i++)
        {
            float u = -f.HalfA + margin + r + (float)rnd.NextDouble() * 2 * (f.HalfA - margin - r);
            float v = -f.HalfD + margin + r + (float)rnd.NextDouble() * 2 * (f.HalfD - margin - r);
            if (f.Free(u, v, r))
            {
                return (u, v);
            }
        }
        return null;
    }

    /// <summary>A column of the material's segments from floor to roof at (u, v), keeping the floor round it.</summary>
    private void Column(Frame f, float u, float v)
    {
        if (!f.Inside(u, v))
        {
            return;
        }
        var p = f.W(u, v, 0);
        float y = f.F, top = f.F + f.H;
        while (y < top - 0.3f)
        {
            var c = _mat.Column.FirstOrDefault(c => c.H <= top - y + 0.01f) ?? _mat.Column[^1];
            _o.Add(c.Prefab, new Vector3(p.X, y + c.Bottom, p.Z), 0);
            y += c.H;
        }
        f.Take(u, v, 0.8f);
    }

    /// <summary>A wall of the material inside a room: across (u0..u1 at depth v) or along (v0..v1 at u), y0..y1 above the floor.</summary>
    private void InnerWall(Frame f, float u0, float v0, float u1, float v1, float y0, float y1)
    {
        var a = f.W(u0, v0, 0);
        var b = f.W(u1, v1, 0);
        bool alongZ = MathF.Abs(a.X - b.X) < 0.01f;
        float line = alongZ ? a.X : a.Z, t0 = alongZ ? Math.Min(a.Z, b.Z) : Math.Min(a.X, b.X), t1 = alongZ ? Math.Max(a.Z, b.Z) : Math.Max(a.X, b.X);
        DungeonShell.Fill(_mat, _o, alongZ, line, t0, t1, f.F + y0, f.F + y1);
    }

    // ---- Set pieces.
    /// <summary>
    /// A raised stone platform 1 m high, w × d metres (even) centred at (u, v), of stone blocks on the marble (through
    /// any wooden floor: stone on wood would not hold); the floor round it kept.
    /// </summary>
    private void Plinth(Frame f, float u, float v, float w, float d)
    {
        for (float du = -w / 2 + 1; du < w / 2; du += 2)
        {
            for (float dv = -d / 2 + 1; dv < d / 2; dv += 2)
            {
                if (f.Inside(u + du, v + dv))
                {
                    _o.Add("stone_floor_2x2", f.W(u + du, v + dv, f.F + 0.5f + Lift), f.Yaw(0));
                }
            }
        }
        f.Take(u, v, MathF.Sqrt(w * w + d * d) / 2 + 0.3f);
    }

    /// <summary>
    /// Steps up a platform's side: a row of stone stairs w metres wide at (u, v), turned to yaw. A stone stair climbs
    /// toward its back, so they rise away from yaw: steps before a platform at +v are turned 180.
    /// </summary>
    private void Steps(Frame f, float u, float v, float yaw, float w)
    {
        for (float du = -w / 2 + 1; du < w / 2; du += 2)
        {
            var (su, sv) = (u + du * MathF.Cos(yaw * MathF.PI / 180), v - du * MathF.Sin(yaw * MathF.PI / 180));
            if (f.Inside(su, sv))
            {
                _o.Add("stone_stair", f.W(su, sv, f.F + Lift), f.Yaw(yaw));
            }
        }
    }

    /// <summary>Pilasters: columns half sunk into the walls every step metres, not by doorways.</summary>
    private void Pilasters(Frame f, float step)
    {
        foreach (var sp in f.WallSpots(step, 2.6f))
        {
            var (u, v) = In(sp, 0.45f);
            Column(f, u, v);
        }
    }

    /// <summary>Cobwebs up in some of the corners, more in a decayed dungeon (from a stream of the room's own: the decay setting moves nothing else).</summary>
    private void Webs(Frame f, float chance)
    {
        var rnd = Local(f, 3);
        string[] webs = ["morkhalla_web_corner", "vertical_web"];
        foreach (var (u, v, yaw, convex) in f.Corners())
        {
            if (convex && rnd.NextDouble() < chance)
            {
                var (cu, cv) = In((u, v, yaw), 0.35f);
                _o.Add(webs[rnd.Next(webs.Length)], f.W(cu, cv, f.Top + f.H - 1.2f - (float)rnd.NextDouble()), f.Yaw(yaw + 180));
            }
        }
    }

    /// <summary>Hooks on chains hanging from the roof of a tall room, here and there (a low room gets none: nothing to hang).</summary>
    private void Chains(Frame f, int n)
    {
        if (f.H < 8)
        {
            return;
        }
        for (int i = 0; i < n; i++)
        {
            if (Spot(f, 0.3f, 1.5f) is var (u, v))
            {
                _o.Add("dvergrprops_hooknchain", f.W(u, v, f.Top + f.H - 2.5f), f.Yaw(R(0, 360)));
            }
        }
    }

    /// <summary>Torches on the walls (sconces) every step metres, at head height; none by doorways.</summary>
    private void WallTorches(Frame f, float step)
    {
        foreach (var sp in f.WallSpots(step, 2.5f))
        {
            var (u, v) = In(sp, 0.25f);
            _o.Add("piece_walltorch", f.W(u, v, f.Top + 2.1f), f.Yaw(sp.Yaw));
        }
    }

    // ---- Light.
    /// <summary>
    /// A random stream of the room's own for one matter (lights, decay), the same whatever the settings for the
    /// others: so that turning the light down or the decay off changes nothing else about the dungeon.
    /// </summary>
    private Random Local(Frame f, int salt) => new(unchecked(_s.Seed * 7919 + f.S.Id * 104729 + salt));

    /// <summary>
    /// How much more a room holds for its depth: a fifth of the top level's foes and loot more for each level down
    /// (linear, not compounding: over sixteen levels the bottom holds four times the top, not fifteen), as dungeons
    /// grow richer and more dangerous the deeper they go.
    /// </summary>
    private static float Depth(Frame f) => 1 + 0.2f * f.S.Level;

    /// <summary>
    /// How a room's purpose lights it: the grand rooms (the entrance, the throne room, the treasury, the feast hall, a
    /// chapel) closer lit, the rooms of the dead and the cells dimmer (and in the dark, Light 0, never lit at all).
    /// </summary>
    private static float LightOf(Room room) => room switch
    {
        Room.Entrance or Room.Throne or Room.Treasury or Room.Feast or Room.Chapel or Room.Boss => 0.75f,
        Room.Ossuary or Room.Tomb or Room.Burial or Room.Cells or Room.Torture or Room.Den or Room.Secret or Room.Store => 1.5f,
        _ => 1,
    };

    /// <summary>The biome's standing torch; a castle torch is now and then unlit in a decayed dungeon (by rnd's chance).</summary>
    private string TorchOf(Random rnd) => _decay > 0 && _b.Torch.StartsWith("CastleKit_groundtorch", StringComparison.Ordinal) && rnd.NextDouble() < _decay * 0.5 ? "CastleKit_groundtorch_unlit" : _b.Torch;

    /// <summary>The biome's brazier at (u, v), if the spot is free (a light's brazier keeps no room for itself: see <see cref="PlaceLights"/>).</summary>
    private void Brazier(Frame f, float u, float v, bool take = true) => Put(f, _b.Brazier, u, v, 0, 0.6f, force: false, take: take);

    /// <summary>
    /// Asks for the room's lights (torches along the walls, closer the brighter the settings' Light; in the dark,
    /// Light 0, most rooms get none unless important). They are placed once the room is furnished.
    /// </summary>
    private void Lights(bool important = false) => _lights = Math.Max(_lights, important ? 2 : 1);

    /// <summary>
    /// The room's lights, after everything else: they take free spots but keep none, so that the settings' Light
    /// changes nothing but them (their stream is the room's own, their spots never move what comes later).
    /// </summary>
    private void PlaceLights(Frame f, bool important)
    {
        var rnd = Local(f, 1);
        if (_s.Light <= 0 && !important && (LightOf(f.S.Room) > 1 || rnd.NextDouble() >= 0.3))
        {
            return;
        }
        float step = (_s.Light >= 2 ? 5 : _s.Light == 1 ? 8 : 12) * LightOf(f.S.Room);
        // How the room is lit: standing torches along the walls, sconces on them, or a brazier or two in the corners.
        switch (rnd.Next(5))
        {
            case 0 or 1 when _mat.Name is "Stone" or "Marble" or "Grausten":
                WallTorches(f, step);
                break;
            case 2 when f.Area >= 40:
                foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex).OrderBy(_ => rnd.Next()).Take(_s.Light >= 2 ? 4 : 2))
                {
                    var (bu, bv) = In((u, v, yaw), 1.1f);
                    Brazier(f, bu, bv, take: false);
                }
                break;
            default:
                string lamp = Lamp(rnd);
                foreach (var s in f.WallSpots(step, 2.5f))
                {
                    var (u, v) = In(s, 0.7f);
                    Put(f, lamp, u, v, s.Yaw, 0.4f, take: false);
                }
                break;
        }
    }

    // ---- What hangs on walls.
    /// <summary>Up to count hangings on the walls, from choice or else the biome's.</summary>
    private void Hangings(Frame f, int count, params string[] choice)
    {
        string[] options = choice.Length > 0 ? choice : _b.Hanging;
        var spots = f.WallSpots(3.2f, 3f).OrderBy(_ => _rnd.Next()).Take(count).ToList();
        string last = "";
        foreach (var s in spots)
        {
            last = Another(options, last);
            Hang(f, last, s);
        }
    }

    /// <summary>Hangs a prefab at a wall spot as <see cref="DungeonProps.Hanging"/> says; nothing when it does not fit.</summary>
    private void Hang(Frame f, string prefab, (float U, float V, float Yaw) s)
    {
        var h = DungeonProps.Hanging(prefab, f.H);
        if (h == null)
        {
            return;
        }
        var (u, v) = In(s, h.Value.Inward);
        _o.Add(prefab, f.W(u, v, f.Top + h.Value.Y), f.Yaw(s.Yaw + h.Value.Turn));
        f.Take(u, v, 0.3f);
    }

    /// <summary>A runner rug down the middle, from v0 to v1 (one small rug when there is no room for two pieces).</summary>
    private void Runner(Frame f, float v0, float v1, float u = 0)
    {
        const float Len = 4.36f;
        int n = (int)MathF.Floor((v1 - v0) / Len);
        if (n < 2)
        {
            Put(f, One("rug_wolf", "rug_fur", "rug_deer"), u, (v0 + v1) / 2, 0, 0, force: true);
            return;
        }
        float start = (v0 + v1) / 2 - n * Len / 2 + Len / 2;
        for (int i = 0; i < n; i++)
        {
            string piece = i == 0 ? "Morkhalla_Rug_end1" : i == n - 1 ? "Morkhalla_Rug_end2" : "Morkhalla_Rug_middle";
            _o.Add(piece, f.W(u, start + i * Len, f.Top + 0.06f), f.Yaw(i == n - 1 ? 180 : 0));
        }
    }

    // ---- What lies about.
    /// <summary>
    /// Things lying about, perArea a square metre (more with Decor), mostly by the walls; from choice or
    /// else the biome's clutter.
    /// </summary>
    private void Clutter(Frame f, float perArea, params string[] choice)
    {
        string[] options = choice.Length > 0 ? choice : _b.Clutter;
        // Some rooms are bare, some cluttered (a density of the room's own), and nothing stands twice running.
        int n = Count(f.Area * perArea * (0.5f + 0.5f * _s.Decor) * R(0.5f, 1.5f));
        var walls = f.WallSpots(1.3f, 2.5f).OrderBy(_ => _rnd.Next()).ToList();
        string last = "";
        for (int i = 0; i < n; i++)
        {
            string thing = Another(options, last);
            if (i < walls.Count && Chance(0.7))
            {
                var (u, v) = In(walls[i], R(0.5f, 1.1f));
                Put(f, thing, u, v, R(0, 360), 0.45f);
            }
            else if (Spot(f, 0.5f) is var (u, v))
            {
                Put(f, thing, u, v, R(0, 360), 0.45f);
            }
            last = thing;
        }
    }

    /// <summary>Asks for the room's decay (rubble, broken walls and pots, a fallen pillar): placed once the room is furnished.</summary>
    private void Decay() => _decayed = true;

    /// <summary>
    /// Rubble in the corners, broken walls and pots, now and then a fallen pillar: more the more decayed. After
    /// everything else, from a stream of the room's own, so that the settings' Decay changes nothing but the decay.
    /// </summary>
    private void PlaceDecay(Frame f)
    {
        if (_decay <= 0)
        {
            return;
        }
        var rnd = Local(f, 2);
        float Rr(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        int Cc(float n) => n <= 0 ? 0 : (int)MathF.Floor(n) + (rnd.NextDouble() < n - MathF.Floor(n) ? 1 : 0);
        string[] rubbles = ["Morkhalla_Rubble2", "Morkhalla_Rubble1", "Morkhalla_Rubble3", "Morkhalla_Rubble1"];
        string[] debrisKinds = ["stone_wall_2x1_ruin", "stone_wall_1x1_ruin", "piece_pot1_cracked", "piece_pot3_cracked"];
        int rubble = Cc(f.Area / 90 * _decay * 2);
        var corners = f.Corners().Where(c => c.Convex).OrderBy(_ => rnd.Next()).ToList();
        for (int i = 0; i < rubble && corners.Count > 0; i++)
        {
            var (u, v, yaw, _) = corners[i % corners.Count];
            var (cu, cv) = In((u, v, yaw), 2.2f);
            for (int t = 0; t < 8 && !Put(f, rubbles[rnd.Next(rubbles.Length)], cu + (t == 0 ? 0 : Rr(-0.6f, 0.6f)), cv + (t == 0 ? 0 : Rr(-0.6f, 0.6f)), Rr(0, 360), 1.8f); t++)
            {
            }
        }
        int debris = Cc(f.Area / 40 * _decay * 2);
        for (int i = 0; i < debris; i++)
        {
            for (int t = 0; t < 30; t++)
            {
                float u = Rr(-f.HalfA + 1.4f, f.HalfA - 1.4f), v = Rr(-f.HalfD + 1.4f, f.HalfD - 1.4f);
                if (Put(f, debrisKinds[rnd.Next(debrisKinds.Length)], u, v, Rr(0, 360), 0.6f))
                {
                    break;
                }
            }
        }
        // A fallen pillar now and then.
        if (_mat.Column[0].Prefab == "stone_pillar" && f.Area > 60 && rnd.NextDouble() < _decay * 0.6)
        {
            for (int t = 0; t < 30; t++)
            {
                float pu = Rr(-f.HalfA + 2.1f, f.HalfA - 2.1f), pv = Rr(-f.HalfD + 2.1f, f.HalfD - 2.1f);
                if (f.Free(pu, pv, 1.3f))
                {
                    var q = DungeonOut.Yaw(f.Yaw(Rr(0, 360))) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
                    _o.Add("stone_pillar", f.W(pu, pv, f.F + 0.55f), q);
                    f.Take(pu, pv, 1.3f);
                    break;
                }
            }
        }
    }

    // ---- Who guards it.
    /// <summary>
    /// A foe at (u, v): its spawner, or the creature itself when once or the settings do not respawn (only
    /// then is level written: 2 is one star).
    /// </summary>
    private void Spawn(Frame f, Foe foe, float u, float v, float yaw, int level = 1, bool once = false, bool force = false)
    {
        bool creature = once || !_s.Respawn;
        var data = creature && level > 1 ? new[] { ("ints", "level", level.ToString(System.Globalization.CultureInfo.InvariantCulture)) } : null;
        Put(f, creature ? foe.Creature : foe.Spawner, u, v, yaw, 0.6f, force: force, data: data);
    }

    /// <summary>Foes about the room, perArea for every 75 square metres times the settings' Monsters; from pool, else the dungeon's.</summary>
    private void Foes(Frame f, float perArea, Foe[]? pool = null, int level = 1)
    {
        int n = Count(f.Area * perArea / 75f * _s.Monsters * Depth(f));
        pool ??= _foes;
        for (int i = 0; i < n && pool.Length > 0; i++)
        {
            // Away from the way in, facing it.
            if (Spot(f, 0.7f, 1.2f) is var (u, v))
            {
                Spawn(f, One(pool), u, v, 180 + R(-40, 40), level);
            }
        }
    }

    /// <summary>One of the biome's elites at (u, v), level 2 when Monsters is 2 or more; none when there are no monsters.</summary>
    private void Elite(Frame f, float u, float v, float yaw)
    {
        if (_s.Monsters > 0 && _b.Elites.Length > 0)
        {
            Spawn(f, One(_b.Elites), u, v, yaw, _s.Monsters >= 2 ? 2 : 1);
        }
    }

    // ---- What it holds.
    /// <summary>The biome's treasure chest at (u, v), unless there is no loot (and it is not forced).</summary>
    private void Chest(Frame f, float u, float v, float yaw, bool force = false)
    {
        if (force || _s.Loot > 0)
        {
            Put(f, One(BiomeChests), u, v, yaw, 0.8f);
        }
    }

    /// <summary>A chest at a free spot along the walls, tried in random order; none when no spot is free.</summary>
    private void ChestAgainstWall(Frame f)
    {
        foreach (var s in f.WallSpots(2, 3f).OrderBy(_ => _rnd.Next()))
        {
            var (u, v) = In(s, 0.6f);
            if (f.Free(u, v, 0.8f))
            {
                Chest(f, u, v, s.Yaw);
                return;
            }
        }
    }

    /// <summary>
    /// A chest holding the boss's key (the crypt key opens the arena's gate) and some loot. Its contents
    /// are written in, and the game told it already has them, or it would fill it with its own.
    /// </summary>
    private void KeyChest(Frame f)
    {
        var items = new List<(string, int)> { ("CryptKey", 1) };
        foreach (var (item, max) in _b.Loot.OrderBy(_ => _rnd.Next()).Take(3))
        {
            items.Add((item, Math.Max(1, (int)(max * R(0.4f, 1)))));
        }
        var data = new[]
        {
            ("ints", "addedDefaultItems", "1"),
            ("bytes", "items", Convert.ToBase64String(DungeonProps.Inventory(items))),
        };
        foreach (var s in f.WallSpots(2, 3f).OrderBy(s => Math.Abs(s.V - f.HalfD)))
        {
            var (u, v) = In(s, 0.6f);
            if (Put(f, _b.Chest, u, v, s.Yaw, 0.8f, data: data))
            {
                Brazier(f, u + 1.6f, v);
                return;
            }
        }
        Put(f, _b.Chest, 0, 0, 0, 0.8f, force: true, data: data);
    }

    /// <summary>About n of the biome's pickable loot lying about, more with the settings' Loot.</summary>
    private void Pickables(Frame f, float n)
    {
        for (int i = 0; i < Count(n * (0.5f + 0.5f * _s.Loot) * Depth(f)); i++)
        {
            if (Spot(f, 0.5f) is var (u, v))
            {
                Put(f, _b.Pickable, u, v, R(0, 360), 0.5f);
            }
        }
    }

    // ---- The rooms.
    /// <summary>Dresses a space for what it is, and puts the key chest and its guard in if it holds the key.</summary>
    private void Furnish(Frame f)
    {
        _lights = 0;
        _decayed = false;
        if (f.S.Corridor)
        {
            Corridor(f);
            Nooks(f);
            Finish(f);
            return;
        }
        if (Sparse(f))
        {
            Bare(f);
            Nooks(f);
            Finish(f);
            return;
        }
        switch (f.S.Room)
        {
            case Room.Entrance: Entrance(f); break;
            case Room.Hall: Hall(f); break;
            case Room.Hub: Hub(f); break;
            case Room.Gallery: Gallery(f); break;
            case Room.Burial: Burial(f); break;
            case Room.Ossuary: Ossuary(f); break;
            case Room.Chapel: Chapel(f); break;
            case Room.Shrine: Shrine(f); break;
            case Room.Tomb: Tomb(f); break;
            case Room.Throne: Throne(f); break;
            case Room.Barracks: Barracks(f); break;
            case Room.Armory: Armory(f); break;
            case Room.Feast: Feast(f); break;
            case Room.Kitchen: Kitchen(f); break;
            case Room.Store: Store(f); break;
            case Room.Forge: Forge(f); break;
            case Room.Library: Library(f); break;
            case Room.Cells: Cells(f); break;
            case Room.Torture: Torture(f); break;
            case Room.Guard: Guard(f); break;
            case Room.Treasury: Treasury(f); break;
            case Room.Boss: Boss(f); break;
            case Room.Antechamber: Antechamber(f); break;
            case Room.Den: Den(f); break;
            case Room.Totem: Totem(f); break;
            case Room.Mine: Mine(f); break;
            case Room.Workshop: Workshop(f); break;
            case Room.Quarters: Quarters(f); break;
            case Room.Secret: Secret(f); break;
            case Room.StairTop or Room.StairBottom: Stairwell(f); break;
        }
        if (f.S.KeyChest)
        {
            KeyChest(f);
            Elite(f, 0, f.HalfD * 0.3f, 180);
        }
        Nooks(f);
        Finish(f);
    }

    /// <summary>
    /// The last touches, once everything that keeps a spot stands: the lights the room asked for, then its decay.
    /// Each from a stream of its own, neither keeping a spot, so that the settings' Light and Decay change nothing
    /// else (decay's rubble may lie over an unkept torch's foot; the torch is thin).
    /// </summary>
    private void Finish(Frame f)
    {
        if (f.S.Corridor)
        {
            CorridorLights(f, Local(f, 1));
            var decay = Local(f, 2);
            if (_decay > 0.3f && decay.NextDouble() < _decay * 0.3 && Spot(f, 0.8f, rnd: decay) is var (ru, rv))
            {
                string[] heaps = ["Morkhalla_Rubble2", "Morkhalla_Rubble3", "Morkhalla_Rubble1"];
                Put(f, heaps[decay.Next(heaps.Length)], ru, rv, (float)decay.NextDouble() * 360, 0.9f, take: false);
            }
            Growths(f);
            return;
        }
        if (_lights > 0)
        {
            PlaceLights(f, _lights == 2);
        }
        // Growths before the decay: neither keeps a spot, and the growths must not move with the Decay setting.
        Growths(f);
        if (_decayed)
        {
            PlaceDecay(f);
        }
    }

    /// <summary>A point d metres along the wall from a spot facing yaw (to its right for d &gt; 0).</summary>
    private static (float U, float V) Beside((float U, float V, float Yaw) s, float d) =>
        (s.U + d * MathF.Cos(s.Yaw * MathF.PI / 180), s.V - d * MathF.Sin(s.Yaw * MathF.PI / 180));

    /// <summary>
    /// A wide alcove's furniture, by the style: a sarcophagus across it with a skull beside, a statue between two
    /// candles, a bed in its bay with a chest at its foot, a shelf and crates, a totem on straw. False when the
    /// style's choice has no place there (the single nook's piece is then tried).
    /// </summary>
    private bool Alcove(Frame f, float u, float v, float yaw)
    {
        // An alcove is a tile deep less its wall: the big piece stands at its middle, a touch out, and is not refused
        // for being near the walls (the planner keeps alcoves clear of doorways and other rooms).
        var spot = (u, v, yaw);
        var (bu, bv) = In(spot, 0.15f);
        switch (_style.Name)
        {
            case "Crypt" or "Catacombs" or "Ruins":
                switch (_rnd.Next(3))
                {
                    case 0:
                        if (!Put(f, "crypt_skeleton_chest", bu, bv, yaw + 90, 0.8f, force: true))
                        {
                            return false;
                        }
                        var (su, sv) = Beside(spot, 1.4f);
                        Put(f, One("skull_pile", "Skull1", "Candle_resin"), su, sv, R(0, 360), 0.3f);
                        return true;
                    case 1:
                        if (!Put(f, _decay > 0.4f ? One("StatueThor_broken_top", "StatueFreya_broken_left") : One("StatueEvil", "StatueThor", "StatueFreya"), bu, bv, yaw, 0.7f, force: true))
                        {
                            return false;
                        }
                        foreach (int side in new[] { -1, 1 })
                        {
                            var (cu, cv) = Beside(spot, side * 1.1f);
                            Put(f, _s.Respawn ? "skull_pile" : "Candle_resin", cu, cv, R(0, 360), 0.25f);
                        }
                        return true;
                    default:
                        foreach (int side in new[] { -1, 1 })
                        {
                            var (ou, ov) = Beside((bu, bv, yaw), side * 0.9f);
                            Put(f, One("bone_stack", "skull_pile", "LargeBone"), ou, ov, R(0, 360), 0.4f);
                        }
                        return true;
                }
            case "Temple":
                if (!Put(f, One("StatueFreya", "StatueThor", "StatueSeed"), bu, bv, yaw, 0.7f, force: true))
                {
                    return false;
                }
                foreach (int side in new[] { -1, 1 })
                {
                    var (cu, cv) = Beside(spot, side * 1.1f);
                    Put(f, _s.Respawn ? "skull_pile" : "Candle_resin", cu, cv, R(0, 360), 0.25f);
                }
                return true;
            case "Fortress" or "Prison":
                if (Chance(0.5))
                {
                    // A sleeping bay: a bed along the back, a chest at its foot.
                    if (!Put(f, "bed", bu, bv, yaw + 90, 0.9f, force: true))
                    {
                        return false;
                    }
                    var (cu, cv) = Beside((u, v, yaw), 1.5f);
                    Put(f, "piece_chest_wood", cu, cv, yaw, 0.4f);
                    return true;
                }
                foreach (int side in new[] { -1, 1 })
                {
                    var (ou, ov) = Beside((bu, bv, yaw), side * 0.9f);
                    Put(f, side < 0 ? "Morkhalla_WeaponStand" : One("ArmorStand_Male", "ArmorStand_Female"), ou, ov, yaw, 0.5f);
                }
                return true;
            case "Dvergr hold":
                if (!Put(f, Chance(0.5) ? "dvergrprops_shelf" : "dvergrprops_bed", bu, bv, yaw + (Chance(0.5) ? 0 : 90), 0.8f, force: true))
                {
                    return false;
                }
                var (ku, kv) = Beside(spot, 1.3f);
                Put(f, One("dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_lantern_standing"), ku, kv, R(0, 360), 0.4f);
                return true;
            case "Goblin warren":
                Put(f, "goblin_strawpile", u, v, R(0, 360), 0, force: true);
                return Put(f, One("goblin_totempole", "goblin_bed", "goblin_trashpile"), bu, bv, yaw, 0.7f, force: true);
            default:
                return false;
        }
    }

    /// <summary>
    /// What stands in the room's alcoves and niches, facing out: in crypts a sarcophagus, bones or a statue; in a
    /// temple a statue with a candle; in a keep or a prison an armour or weapon stand or a barrel; in a hold crates
    /// and a lantern; in a warren a totem or straw. A brazier now and then, whatever the style. A wide alcove gets
    /// the bigger things (a sarcophagus across it, a bench, a pair of statues) with a candle or a skull beside.
    /// </summary>
    private void Nooks(Frame f)
    {
        foreach (var (u, v, yaw, width) in f.Nooks())
        {
            // A corridor's niche hides a foe now and then: the ambush from the blind spot.
            if (f.S.Corridor && _s.Monsters > 0 && _foes.Length > 0 && Chance(0.25))
            {
                Spawn(f, One(_foes), u, v, yaw, force: true);
                continue;
            }
            if (Chance(0.2))
            {
                Brazier(f, u, v);
                continue;
            }
            if (width >= 2 && Alcove(f, u, v, yaw))
            {
                continue;
            }
            switch (_style.Name)
            {
                case "Crypt" or "Catacombs" or "Ruins":
                    Put(f, One("crypt_skeleton_chest", "crypt_skeleton_chest", "skull_pile", "bone_stack", "StatueEvil", "StatueDeer"), u, v, yaw + 90, 0.6f);
                    break;
                case "Temple":
                    if (Put(f, One("StatueFreya", "StatueThor", "StatueSeed"), u, v, yaw, 0.6f) && !_s.Respawn)
                    {
                        _o.Add("Candle_resin", f.W(u + 0.6f, v + 0.4f, f.Top + Lift), f.Yaw(R(0, 360)));
                    }
                    break;
                case "Fortress" or "Prison":
                    Put(f, One("ArmorStand_Male", "Morkhalla_WeaponStand", "barrell", "piece_chest_wood"), u, v, yaw, 0.6f);
                    break;
                case "Dvergr hold":
                    Put(f, One("dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_lantern_standing", "dvergrprops_crate_long"), u, v, yaw, 0.6f);
                    break;
                case "Goblin warren":
                    Put(f, One("goblin_totempole", "goblin_strawpile", "skull_pile", "goblin_trashpile"), u, v, yaw, 0.6f);
                    break;
                default:
                    Put(f, One("skull_pile", "barrell", "StatueDeer"), u, v, yaw, 0.6f);
                    break;
            }
        }
    }

    /// <summary>The wall hangings that suit the style: banners in keeps and temples, the biome's own elsewhere.</summary>
    private string[] Drapes => _style.Name switch
    {
        "Fortress" or "Temple" => ["piece_banner01", "piece_banner02", "piece_banner05", "piece_banner06", "piece_banner07", "cloth_hanging_door"],
        _ => _b.Hanging,
    };

    /// <summary>
    /// A corridor: torches on alternate sides, or sconces; a few things lying about; now and then pilasters with an
    /// arch every 8 m, a niche's statue, rubble, cobwebs, a foe.
    /// </summary>
    private void Corridor(Frame f)
    {
        if (f.S.DeadEnd)
        {
            Cache(f);
        }
        // Pilasters every 8 m along a long straight corridor, a beam of wall between them like an arch.
        if (f.S.Shape == DungeonPlan.Shape.Rect && f.HalfD >= 6 && Chance(0.45))
        {
            for (float v = -f.HalfD + 4; v <= f.HalfD - 4; v += 8)
            {
                if (f.NearDoor(f.HalfA, v, 3) || f.NearDoor(-f.HalfA, v, 3))
                {
                    continue;
                }
                Column(f, -(f.HalfA - 0.45f), v);
                Column(f, f.HalfA - 0.45f, v);
                InnerWall(f, -f.HalfA, v, f.HalfA, v, 3, f.H);
            }
        }
        CorridorKind(f);
        // A statue in a bay, a few bones, rubble, cobwebs.
        if (Chance(0.25) && Spot(f, 0.8f, 0.6f) is var (su, sv))
        {
            Put(f, _decay > 0.4f ? One("StatueThor_broken_top", "StatueFreya_broken_left") : One("StatueThor", "StatueFreya", "StatueDeer"), su, sv, R(0, 360), 0.9f);
        }
        Clutter(f, 0.02f);
        Webs(f, 0.1f + _decay * 0.4f);
        Foes(f, 0.25f);
        // Its lights and a heap of rubble, last, each from a stream of the corridor's own (see Finish).
        _lights = 1;
        _decayed = true;
    }

    /// <summary>
    /// A corridor's lights: torches along the walls (where there are walls: not where another corridor opens in),
    /// every other spot, closer the brighter; sconces instead, now and then, on stone.
    /// </summary>
    private void CorridorLights(Frame f, Random light)
    {
        // Catacombs and prisons keep some corridors dark between their lit rooms (the contrast is the point), unless Bright.
        if (_style.Name is "Catacombs" or "Prison" && _s.Light < 2 && light.NextDouble() < 0.4)
        {
            return;
        }
        float step = _s.Light >= 2 ? 6 : _s.Light == 1 ? 9 : 16;
        bool sconces = _mat.Name is "Stone" or "Marble" or "Grausten" && light.NextDouble() < 0.4;
        string lamp = Lamp(light);
        int k = light.Next(2);
        foreach (var sp in f.WallSpots(step / 2, 2.5f).Where(sp => sp.Yaw is 90 or -90))
        {
            if (k++ % 2 != 0)
            {
                continue;
            }
            if (sconces)
            {
                var (wu, wv) = In(sp, 0.25f);
                _o.Add("piece_walltorch", f.W(wu, wv, f.Top + 2.1f), f.Yaw(sp.Yaw));
            }
            else
            {
                var (tu, tv) = In(sp, 0.5f);
                Put(f, lamp, tu, tv, sp.Yaw, 0.35f, take: false);
            }
        }
    }

    /// <summary>The top or the foot of the stairs: the stairs' side kept free, torches on the other.</summary>
    private void Stairwell(Frame f)
    {
        // The stairs keep the side at u -3.5..-1.5; torches on the other.
        f.Take(-2.5f, 0, 2f);
        f.Take(-2.5f, f.HalfD - 4, 2f);
        f.Take(-2.5f, -f.HalfD + 4, 2f);
        if (f.S.Room == Room.StairTop)
        {
            // Only its first cell has a floor.
            Put(f, _b.Torch, f.HalfA - 0.6f, -f.HalfD + 1, -90, 0.4f);
            Put(f, _b.Torch, -f.HalfA + 0.6f, -f.HalfD + 1, 90, 0.4f);
        }
        else
        {
            Put(f, _b.Torch, f.HalfA - 0.6f, f.HalfD - 1, -90, 0.4f);
            Put(f, _b.Torch, f.HalfA - 0.6f, -f.HalfD + 6, -90, 0.4f);
            Clutter(f, 0.02f);
        }
    }

    /// <summary>
    /// The entrance hall: room for the portal, the name sign, a runner, braziers, benches; or a guard post, with
    /// weapon stands, armour and banners by the way in.
    /// </summary>
    private void Entrance(Frame f)
    {
        // The portal stands before the back wall; the dungeon's name on a sign beside it.
        f.Take(0, -f.HalfD + 1.2f, 1.9f);
        _o.Add("sign", f.W(2.6f, -f.HalfD + 0.04f, f.Top + 2.1f), f.Yaw(DungeonProps.Turn("sign")),
            [("strings", "text", _name)]);
        if (Chance(0.5))
        {
            Runner(f, -f.HalfD + 2.5f, f.HalfD - 1);
            Brazier(f, -2.8f, f.HalfD - 1.3f);
            Brazier(f, 2.8f, f.HalfD - 1.3f);
            foreach (int side in new[] { -1, 1 })
            {
                Put(f, _mat.Name is "Marble" or "Grausten" ? "piece_blackmarble_bench" : "piece_bench01", side * (f.HalfA - 0.6f), 0, side < 0 ? 90 : -90, 1.3f);
            }
        }
        else
        {
            Brazier(f, -2.2f, -f.HalfD + 3.2f);
            Brazier(f, 2.2f, -f.HalfD + 3.2f);
            Put(f, "Morkhalla_WeaponStand", -(f.HalfA - 0.8f), 1, 90, 0.8f);
            Put(f, "ArmorStand_Male", f.HalfA - 0.8f, 1, -90, 0.6f);
            Put(f, "ArmorStand_Female", f.HalfA - 0.8f, 2.4f, -90, 0.6f);
            Put(f, "piece_logbench01", -(f.HalfA - 0.7f), f.HalfD - 2, 90, 0.6f);
            Put(f, One("rug_wolf", "rug_deer", "jute_carpet"), 0, 1, 0, 0, force: true);
            Pilasters(f, 4);
        }
        Hangings(f, 2, Drapes);
        Clusters(f, 1, StyleClusters);
        Lights(important: true);
        Clutter(f, 0.01f);
    }

    /// <summary>
    /// A great hall, one of several: colonnades with a set piece at the far end; a mead hall round a fire; a hall of
    /// the dead with a sarcophagus on a dais between the columns; a bare echoing hall with its pillars fallen.
    /// </summary>
    private void Hall(Frame f)
    {
        bool cols = f.HalfA >= 5;
        int variant = _rnd.Next(cols ? 4 : 2);
        switch (variant)
        {
            case 0:
                // Colonnades, a feature at the far end, braziers and banners between the columns.
                if (Chance(0.5))
                {
                    Loft(f);
                }
                if (cols)
                {
                    for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
                    {
                        Pillar(f, -(f.HalfA - 2.2f), v);
                        Pillar(f, f.HalfA - 2.2f, v);
                    }
                }
                Feature(f, f.HalfD - 2.2f);
                Runner(f, -f.HalfD + 1.5f, f.HalfD - 4.2f);
                for (float v = -f.HalfD + 5; v <= f.HalfD - 5; v += 8)
                {
                    Brazier(f, -(f.HalfA - 1.2f), v);
                    Brazier(f, f.HalfA - 1.2f, v);
                }
                Hangings(f, (int)(f.HalfD / 3), Drapes);
                break;
            case 1:
                // A mead hall: a fire in the middle, benches round it, trestle tables along the sides, trophies.
                Put(f, One("prop_bonfire", "Morkhalla_firepit"), 0, -1, R(0, 360), 1.6f);
                foreach (int side in new[] { -1, 1 })
                {
                    Put(f, "piece_bench01", side * 2.6f, -1, side < 0 ? 90 : -90, 0.5f);
                    Put(f, "piece_logbench01", 0, -1 + side * 2.6f, side < 0 ? 0 : 180, 0.5f);
                }
                for (float v = 2.5f; v <= f.HalfD - 2.5f; v += 4)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        if (Put(f, "piece_table", side * (f.HalfA - 1.4f), v, 90, 1.1f))
                        {
                            Put(f, Chance(0.6) ? FeastFood : "prop_Tankard", side * (f.HalfA - 1.4f), v, R(0, 360), 0, y: 0.82f, force: true);
                        }
                    }
                }
                Trophies(f, 3);
                Hangings(f, (int)(f.HalfD / 3), Drapes);
                Pilasters(f, 8);
                break;
            case 2:
                // The hall of the dead: a dais at the far end bearing a sarcophagus between two statues, columns.
                if (Chance(0.4))
                {
                    Loft(f);
                }
                Plinth(f, 0, f.HalfD - 3, 6, 4);
                Steps(f, 0, f.HalfD - 6, 180, 6);
                _o.Add("crypt_skeleton_chest", f.W(0, f.HalfD - 3, f.F + 1 + Lift * 2), f.Yaw(90));
                string[] statues = _decay > 0.4f ? ["StatueFreya_broken_left", "StatueThor_broken_top"] : ["StatueFreya", "StatueThor"];
                _o.Add(statues[0], f.W(-2.3f, f.HalfD - 2.4f, f.F + 1 + Lift * 2), f.Yaw(180));
                _o.Add(statues[1], f.W(2.3f, f.HalfD - 2.4f, f.F + 1 + Lift * 2), f.Yaw(180));
                for (float v = -f.HalfD + 3; v <= f.HalfD - 8; v += 4)
                {
                    Pillar(f, -(f.HalfA - 2.2f), v);
                    Pillar(f, f.HalfA - 2.2f, v);
                    Put(f, "skull_pile", -(f.HalfA - 0.7f), v + 2, R(0, 360), 0.5f);
                    Put(f, "skull_pile", f.HalfA - 0.7f, v + 2, R(0, 360), 0.5f);
                }
                Brazier(f, -3.4f, f.HalfD - 6.8f);
                Brazier(f, 3.4f, f.HalfD - 6.8f);
                Runner(f, -f.HalfD + 1.5f, f.HalfD - 7.2f);
                Hangings(f, 2);
                Chains(f, 2);
                break;
            default:
                // Bare and echoing: a few columns standing, some fallen, rubble, the far end's altar cracked.
                for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        if (Chance(0.6))
                        {
                            Column(f, side * (f.HalfA - 2.2f), v);
                        }
                        else if (Spot(f, 1.3f) is var (pu, pv))
                        {
                            var q = DungeonOut.Yaw(f.Yaw(R(0, 360))) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
                            _o.Add(_mat.Column[0].Prefab == "stone_pillar" ? "stone_pillar" : "Morkhalla_Rubble4", f.W(pu, pv, f.F + 0.55f), q);
                            f.Take(pu, pv, 1.3f);
                        }
                    }
                }
                Put(f, _b.Name == "Ashlands" ? "blackmarble_altar_crystal_broken" : "StatueThor_broken_bottom", 0, f.HalfD - 2, 180, 1.4f);
                Clutter(f, 0.02f, "Morkhalla_Rubble2", "Morkhalla_Rubble1", "stone_wall_2x1_ruin", "Skull1", "LargeBone");
                Webs(f, 0.7f);
                break;
        }
        if (f.S.Tall)
        {
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add(_b.Name == "Ashlands" ? "CharredBanner1" : "cloth_hanging_long", f.W(side * 3f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
        }
        Clusters(f, 2, StyleClusters);
        Lights();
        Foes(f, 1.1f);
        Clutter(f, 0.012f);
        Decay();
    }

    /// <summary>The set piece at the far end of a hall: an altar, a throne, a statue, a table.</summary>
    private void Feature(Frame f, float v)
    {
        switch (_style.Name)
        {
            case "Fortress" or "Goblin warren":
                Dais(f, v);
                break;
            case "Dvergr hold":
                Put(f, "dvergrprops_table", 0, v - 0.6f, 0, 1.2f);
                Put(f, "dvergrprops_chair", 0, v + 0.3f, 180, 0.4f);
                Put(f, "dvergrprops_lantern_standing", 0.5f, v - 0.6f, 0, 0, y: 0.78f, force: true);
                break;
            default:
                if (!(Chance(0.4) && PutHero(f, 0, v, 180)))
                {
                    Put(f, f.S.Tall ? "StatueEvil" : One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom"), 0, v, 180, 1.6f);
                }
                Brazier(f, -2.2f, v - 0.6f);
                Brazier(f, 2.2f, v - 0.6f);
                break;
        }
    }

    /// <summary>A throne that suits the material (a bone throne in a stone crypt).</summary>
    private string ThronePiece => _mat.Name switch
    {
        "Marble" or "Grausten" => "piece_blackmarble_throne",
        "Goblin" => "piece_bone_throne",
        "Stave" => "piece_moose_throne",
        _ => _style.Name == "Crypt" ? "piece_bone_throne" : One("piece_throne01", "piece_throne02"),
    };

    /// <summary>
    /// A dais of stone 1 m high across the far end, steps in front, the throne on it. It stands on the
    /// marble, through any wooden floor (stone on wood would not hold).
    /// </summary>
    private void Dais(Frame f, float v)
    {
        float back = Math.Min(f.HalfD - 0.6f, v + 1.2f);
        for (float u = -2; u <= 2.01f; u += 2)
        {
            for (float dv = 0; dv < 4; dv += 2)
            {
                _o.Add("stone_floor_2x2", f.W(u, back - 1 - dv, f.F + 0.5f + Lift), f.Yaw(0));
            }
        }
        for (float u = -2; u <= 2.01f; u += 2)
        {
            _o.Add("stone_stair", f.W(u, back - 5, f.F + Lift), f.Yaw(180));
        }
        f.Take(0, back - 2.5f, 3.6f);
        _o.Add(ThronePiece, f.W(0, back - 1.2f, f.F + 1 + Lift * 2), f.Yaw(180 + DungeonProps.Turn(ThronePiece)));
        Brazier(f, -3.6f, back - 1.5f);
        Brazier(f, 3.6f, back - 1.5f);
    }

    /// <summary>A crossing: a statue, a ring of braziers, a column or an altar in the middle; columns round it if large.</summary>
    private void Hub(Frame f)
    {
        switch (_rnd.Next(4))
        {
            case 0:
                if (!(Chance(0.35) && PutHero(f, 0, 0, 180, 1.8f)))
                {
                    Put(f, _decay > 0.4f ? One("StatueThor_broken_top", "StatueFreya_broken_left") : One("StatueThor", "StatueFreya", "StatueDeer", "StatueSeed"), 0, 0, 180, 1.4f);
                }
                break;
            case 1:
                for (int i = 0; i < 4; i++)
                {
                    float a = i * MathF.PI / 2 + MathF.PI / 4;
                    Brazier(f, 1.6f * MathF.Cos(a), 1.6f * MathF.Sin(a));
                }
                Put(f, "Morkhalla_giant_railing_deco", 0, 0, 0, 0.8f, y: 0.5f);
                break;
            case 2:
                Column(f, 0, 0);
                break;
            default:
                Put(f, "blackmarble_altar_crystal", 0, 0, 0, 1f, y: 0.9f);
                break;
        }
        if (f.S.Shape == DungeonPlan.Shape.Cross)
        {
            // Pilasters where the notches meet the walls.
            foreach (var (u, v, yaw, convex) in f.Corners().Where(c => !c.Convex))
            {
                var (cu, cv) = In((u, v, yaw), 0.5f);
                Column(f, cu, cv);
            }
        }
        else if (f.HalfA >= 5 && f.HalfD >= 5)
        {
            foreach (var (u, v) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
            {
                if (!f.NearDoor(u, v, 3))
                {
                    Column(f, u, v);
                }
            }
        }
        Hangings(f, 2, Drapes);
        Clusters(f, 1, StyleClusters);
        Lights();
        Foes(f, 0.6f);
        Clutter(f, 0.015f);
        Decay();
    }

    /// <summary>
    /// A gallery, one of several: the dead in niches between pilasters; sarcophagi standing along the walls; statues and
    /// braziers in turn; or bare, cobwebbed, with rubble.
    /// </summary>
    private void Gallery(Frame f)
    {
        string[] remains = [.. _b.Clutter.Where(c => c.Contains("Remains", StringComparison.Ordinal) || c == "Skull1" || c == "LargeBone")];
        if (remains.Length == 0)
        {
            remains = ["Skull1", "LargeBone"];
        }
        int variant = _rnd.Next(4);
        for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 2)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float u = side * (f.HalfA - 0.6f);
                if (f.NearDoor(u, v, 2.6f))
                {
                    continue;
                }
                int slot = (int)((v + f.HalfD) / 2) % 3;
                switch (variant)
                {
                    case 0 when slot == 0:
                        Column(f, side * (f.HalfA - 0.5f), v);
                        break;
                    case 0:
                        if (Chance(0.7))
                        {
                            Put(f, One(remains), u, v, side < 0 ? 90 : -90, 0.5f);
                        }
                        break;
                    case 1 when slot == 1:
                        Put(f, "crypt_skeleton_chest", side * (f.HalfA - 1.1f), v, side < 0 ? 0 : 180, 1.1f);
                        break;
                    case 1 when slot == 0:
                        Column(f, side * (f.HalfA - 0.5f), v);
                        break;
                    case 2 when slot == 1:
                        Put(f, One("StatueThor", "StatueFreya", "StatueDeer", "StatueEvil"), side * (f.HalfA - 0.9f), v, side < 0 ? 90 : -90, 0.8f);
                        break;
                    case 2 when slot == 0 && Chance(0.5):
                        Brazier(f, side * (f.HalfA - 1f), v);
                        break;
                    case 3 when slot == 0:
                        Put(f, One("Morkhalla_Rubble2", "skull_pile", "bone_stack"), u, v, R(0, 360), 0.5f);
                        break;
                }
            }
        }
        if (variant == 3)
        {
            Webs(f, 0.8f);
            Chains(f, 1);
        }
        Lights();
        Foes(f, 0.8f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>
    /// A burial chamber, one of several: rows of sarcophagi each side of an aisle; one tomb on a dais in the middle,
    /// candles and skulls about it; sarcophagi set at odd angles among the bones of a catacomb. Sleeping draugr in
    /// the swamp.
    /// </summary>
    private void Burial(Frame f)
    {
        switch (_rnd.Next(f.HalfA >= 3.4f && f.HalfD >= 3.4f ? 4 : 3))
        {
            case 3:
                // The vault: an inner chamber against the far wall holding the sarcophagus, the dead's goods about it.
                Vault(f, 4, 3.5f);
                _o.Add("crypt_skeleton_chest", f.W(0, f.HalfD - 1.75f, f.Top + Lift), f.Yaw(90));
                f.Take(0, f.HalfD - 1.75f, 1.3f);
                Put(f, One("piece_chest_wood", "barrell", "piece_pot2_cracked"), -1.2f, f.HalfD - 0.7f, R(0, 360), 0.3f, force: true);
                Put(f, "skull_pile", 1.3f, f.HalfD - 0.7f, R(0, 360), 0.3f, force: true);
                Brazier(f, -(f.HalfA - 1), -(f.HalfD - 1));
                Brazier(f, f.HalfA - 1, -(f.HalfD - 1));
                Clutter(f, 0.02f, "Skull1", "LargeBone", "bone_stack");
                break;
            case 0:
                float uRow = Math.Max(2.2f, f.HalfA / 2 + 0.3f);
                for (float v = -f.HalfD + 2.2f; v <= f.HalfD - 1.6f; v += 2.4f)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Put(f, "crypt_skeleton_chest", side * uRow, v, side < 0 ? 0 : 180, 1.2f);
                    }
                }
                break;
            case 1:
                Plinth(f, 0, 0.5f, 4, 4);
                _o.Add("crypt_skeleton_chest", f.W(0, 0.5f, f.F + 1 + Lift * 2), f.Yaw(90));
                Steps(f, 0, -2.5f, 180, 2);
                foreach (var (du, dv) in new[] { (-2.6f, -2.6f), (2.6f, -2.6f), (-2.6f, 3.6f), (2.6f, 3.6f) })
                {
                    Put(f, "skull_pile", du, dv, R(0, 360), 0.5f);
                }
                if (!_s.Respawn)
                {
                    foreach (var (du, dv) in new[] { (-1.6f, -1.6f), (1.6f, -1.6f), (-1.6f, 2.6f), (1.6f, 2.6f) })
                    {
                        _o.Add("Candle_resin", f.W(du, dv, f.F + 1 + Lift * 2), f.Yaw(R(0, 360)));
                    }
                }
                Brazier(f, -(f.HalfA - 1), f.HalfD - 1);
                Brazier(f, f.HalfA - 1, f.HalfD - 1);
                Chains(f, 2);
                break;
            default:
                for (int i = 0; i < Count(f.Area / 14); i++)
                {
                    if (Spot(f, 1.2f, 0.6f) is var (u, v))
                    {
                        Put(f, "crypt_skeleton_chest", u, v, R(0, 360), 1.2f);
                    }
                }
                Clutter(f, 0.03f, "Skull1", "LargeBone", "bone_stack", "skull_pile");
                Webs(f, 0.6f);
                break;
        }
        foreach (var (cu, cv) in new[] { (-1, 1), (1, 1) })
        {
            Put(f, "skull_pile", cu * (f.HalfA - 0.7f), cv * (f.HalfD - 0.7f), R(0, 360), 0.6f);
        }
        Clusters(f, 1, "bones", "altar");
        Lights();
        Hangings(f, 1);
        // The dead do not rest here.
        var sleepers = _b.Name == "Swamp" ? new[] { new Foe("Spawner_Draugr", "Draugr_sleeping"), new Foe("Spawner_Draugr_Ranged", "Draugr_Ranged_sleeping") } : null;
        Foes(f, 1.1f, sleepers);
        Clutter(f, 0.02f);
        Decay();
    }

    /// <summary>
    /// An ossuary: bones along every wall, or a great heap in the middle with skulls in the corners, or stacked in rows
    /// forming aisles; with monsters, a bone pile that raises the dead.
    /// </summary>
    private void Ossuary(Frame f)
    {
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (var s in f.WallSpots(1.25f, 2.6f))
                {
                    var (u, v) = In(s, 0.55f);
                    Put(f, Chance(0.5) ? "skull_pile" : "bone_stack", u, v, s.Yaw + R(-20, 20), 0.5f);
                }
                break;
            case 1:
                for (int i = 0; i < 7; i++)
                {
                    PutNear(f, One("bone_stack", "skull_pile", "LargeBone"), R(-1.5f, 1.5f), R(-1.5f, 1.5f), R(0, 360), 0.35f, 1f);
                }
                foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex))
                {
                    var (cu, cv) = In((u, v, yaw), 0.9f);
                    Put(f, "skull_pile", cu, cv, R(0, 360), 0.5f);
                }
                Webs(f, 0.7f);
                break;
            default:
                for (float u = -f.HalfA + 1.5f; u <= f.HalfA - 1.5f; u += 3)
                {
                    for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 1.3f)
                    {
                        Put(f, Chance(0.8) ? "bone_stack" : "skull_pile", u, v, 90, 0.5f);
                    }
                }
                break;
        }
        if (_s.Monsters > 0)
        {
            // A heap of bones that keeps raising the dead until broken.
            Put(f, _b.Name == "Swamp" ? "Spawner_DraugrPile" : _b.Name is "Black Forest" or "Meadows" ? "BonePileSpawner" : "BonePileSpawner_swamp", 0, R(-1, 1), R(0, 360), 1.3f);
        }
        Clutter(f, 0.04f, "Skull1", "LargeBone", "LargeBone_half01", "Skull1");
        Clusters(f, 1, "bones");
        Lights();
        Foes(f, 0.4f);
        Decay();
    }

    /// <summary>
    /// A chapel: an altar between two statues at the far end with pews facing it; or a round shrine, the altar in the
    /// middle in a ring of braziers; or a bare nave of rugs and candles under long hangings.
    /// </summary>
    private void Chapel(Frame f)
    {
        float far = f.HalfD - 1.6f;
        string altar = One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom");
        string[] statues = _decay > 0.4f ? ["StatueFreya_broken_left", "StatueThor_broken_top"] : ["StatueFreya", "StatueThor"];
        string bench = _mat.Name is "Marble" or "Grausten" ? "piece_blackmarble_bench" : _mat.Name == "Stave" ? "prop_piece_bench_runed" : "piece_bench01";
        int variant = _rnd.Next(3);
        switch (variant)
        {
            case 0:
                if (Chance(0.4))
                {
                    Loft(f);
                }
                Put(f, altar, 0, far, 180, 1.4f);
                Put(f, statues[0], -2.6f, far + 0.3f, 180, 0.6f);
                Put(f, statues[1], 2.6f, far + 0.3f, 180, 0.6f);
                Brazier(f, -1.6f, far - 1.8f);
                Brazier(f, 1.6f, far - 1.8f);
                for (float v = -f.HalfD + 3; v <= far - 3.5f; v += 1.7f)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        for (float u = 2.6f; u <= f.HalfA - 1.2f; u += 2.6f)
                        {
                            Put(f, bench, side * u, v, 0, 0.7f);
                        }
                    }
                }
                Runner(f, -f.HalfD + 1.2f, far - 1.4f);
                break;
            case 1:
                Plinth(f, 0, 0.5f, 4, 4);
                _o.Add(altar, f.W(0, 0.5f, f.F + 1 + Lift * 2), f.Yaw(180));
                Steps(f, 0, -2.5f, 180, 2);
                float r = Math.Min(f.HalfA, f.HalfD) - 1.4f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3 + MathF.PI / 6;
                    Brazier(f, r * MathF.Cos(a), 0.5f + r * MathF.Sin(a));
                }
                Put(f, statues[0], -(f.HalfA - 1), far + 0.3f, 180, 0.6f);
                Put(f, statues[1], f.HalfA - 1, far + 0.3f, 180, 0.6f);
                Pilasters(f, 4);
                break;
            default:
                Put(f, altar, 0, far, 180, 1.4f);
                Put(f, "StatueEvil", 0, far - 2.4f, 180, 1f);
                for (float v = -f.HalfD + 2.5f; v <= far - 4; v += 3)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Put(f, One("jute_carpet", "jute_carpet_blue", "rug_fur"), side * 2.2f, v, 0, 0, force: true);
                    }
                }
                if (!_s.Respawn)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (Spot(f, 0.2f, 0.5f) is var (cu, cv))
                        {
                            Put(f, "Candle_resin", cu, cv, R(0, 360), 0.2f);
                        }
                    }
                }
                Chains(f, 2);
                break;
        }
        Pickables(f, 0.5f);
        if (f.S.Tall)
        {
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add("cloth_hanging_long", f.W(side * 2.2f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
        }
        Hangings(f, 2, Drapes);
        Clusters(f, 1, "altar");
        Lights(important: true);
        Foes(f, 0.7f);
        Clutter(f, 0.008f);
        Decay();
    }

    /// <summary>
    /// A shrine: a statue or altar in a ring of braziers and skull piles; or a dolmen on a dais; or a statue on a
    /// plinth with offerings heaped before it.
    /// </summary>
    private void Shrine(Frame f)
    {
        switch (_rnd.Next(_mat.Name is "Stone" or "Grausten" or "Marble" ? 4 : 3))
        {
            case 3:
                // Sunken: ledges round a lower floor, the idol down in the middle in a ring of candles or skulls.
                Ledges(f);
                Put(f, One("StatueSeed", "StatueEvil", "StatueDeer", "MountainGraveStone01"), 0, 0, 180, 1.2f, force: true);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3;
                    Put(f, _s.Respawn ? "skull_pile" : "Candle_resin", 1.7f * MathF.Cos(a), 1.7f * MathF.Sin(a), R(0, 360), 0.2f);
                }
                break;
            case 0:
                Put(f, f.S.Tall ? "StatueEvil" : One("StatueSeed", "blackmarble_altar_crystal", "StatueDeer"), 0, 0, 180, 1.3f, y: 0);
                float r = Math.Min(f.HalfA, f.HalfD) - 1.6f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3;
                    if (i % 2 == 0)
                    {
                        Brazier(f, r * MathF.Cos(a), r * MathF.Sin(a));
                    }
                    else
                    {
                        Put(f, "skull_pile", r * MathF.Cos(a), r * MathF.Sin(a), R(0, 360), 0.6f);
                    }
                }
                break;
            case 1 when _b.Name == "Mistlands":
                // A wisp fountain in the middle, black cores and candles round it.
                Put(f, "piece_wisplure", 0, 0, 0, 1.4f, force: true);
                Cores(f, 2);
                Brazier(f, -(f.HalfA - 1.1f), -(f.HalfD - 1.1f));
                Brazier(f, f.HalfA - 1.1f, -(f.HalfD - 1.1f));
                break;
            case 1 when _b.Name is "Deep North" or "Mountain":
                // A memorial stone on a plinth, snow lanterns before it.
                Plinth(f, 0, 0.5f, 2, 2);
                _o.Add(One("MemorialStone_Medium", "MemorialStone_Large"), f.W(0, 0.5f, f.F + 1 + Lift * 2 + DungeonProps.Foot("MemorialStone_Medium")), f.Yaw(180));
                Steps(f, 0, -1.5f, 180, 2);
                Put(f, "piece_snowlantern", -1.6f, -1.6f, 0, 0.3f);
                Put(f, "piece_snowlantern", 1.6f, -1.6f, 0, 0.3f);
                Pilasters(f, 4);
                break;
            case 1 when f.HalfA >= 3.4f:
                // A standing stone: a wide one lying across the middle, or a high one where the roof allows.
                Put(f, f.S.Tall ? One("highstone", "highstone_2") : One("widestone", "widestone_2"), 0, 0, R(0, 360), 2.2f, force: true);
                Brazier(f, -(f.HalfA - 1.1f), -(f.HalfD - 1.1f));
                Brazier(f, f.HalfA - 1.1f, -(f.HalfD - 1.1f));
                Pilasters(f, 4);
                break;
            case 1:
                Plinth(f, 0, 0, 4, 4);
                _o.Add(One("RockDolmen_1", "RockDolmen_2", "RockDolmen_3"), f.W(0, 0, f.F + 1 + Lift * 2 + DungeonProps.Foot("RockDolmen_1")), f.Yaw(R(0, 360)));
                Steps(f, 0, -3, 180, 2);
                Brazier(f, -(f.HalfA - 1.1f), -(f.HalfD - 1.1f));
                Brazier(f, f.HalfA - 1.1f, -(f.HalfD - 1.1f));
                Pilasters(f, 4);
                break;
            default:
                Plinth(f, 0, f.HalfD - 2, 2, 2);
                _o.Add(One("StatueThor", "StatueFreya", "StatueEvil"), f.W(0, f.HalfD - 2, f.F + 1 + Lift * 2), f.Yaw(180));
                for (int i = 0; i < Count(2 + _s.Loot * 2); i++)
                {
                    PutNear(f, "treasure_stack", R(-1.5f, 1.5f), f.HalfD - 4.2f, R(0, 360), 0.3f, 0.8f);
                }
                Brazier(f, -2, f.HalfD - 2.4f);
                Brazier(f, 2, f.HalfD - 2.4f);
                Put(f, One("rug_wolf", "rug_fur", "rug_deer"), 0, f.HalfD - 4.6f, 0, 0, force: true);
                Chains(f, 1);
                break;
        }
        Pickables(f, 0.6f);
        Clusters(f, 1, "altar");
        Hangings(f, 2);
        Foes(f, 0.9f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>
    /// A tomb: a sarcophagus and grave stone among four columns; or on a dais between two statues, chains above;
    /// treasure, an elite guard.
    /// </summary>
    private void Tomb(Frame f)
    {
        if (Chance(0.5))
        {
            Put(f, "crypt_skeleton_chest", 0, 0.5f, 90, 1.6f);
            Put(f, "MountainGraveStone01", 0, 2.6f, 180, 0.6f);
            foreach (var (u, v) in new[] { (-2.3f, -1.6f), (2.3f, -1.6f), (-2.3f, 2.6f), (2.3f, 2.6f) })
            {
                Column(f, u, v);
            }
        }
        else
        {
            Plinth(f, 0, 1, 4, 4);
            _o.Add("crypt_skeleton_chest", f.W(0, 1, f.F + 1 + Lift * 2), f.Yaw(90));
            Steps(f, 0, -2, 180, 2);
            string[] statues = _decay > 0.4f ? ["StatueFreya_broken_left", "StatueThor_broken_top"] : ["StatueFreya", "StatueThor"];
            Put(f, statues[0], -(f.HalfA - 1), f.HalfD - 1, 180, 0.6f);
            Put(f, statues[1], f.HalfA - 1, f.HalfD - 1, 180, 0.6f);
            Chains(f, 2);
        }
        Brazier(f, -f.HalfA + 1, -f.HalfD + 1);
        Brazier(f, f.HalfA - 1, -f.HalfD + 1);
        for (int i = 0; i < Count(1 + _s.Loot); i++)
        {
            PutNear(f, "treasure_stack", R(-1, 1), -1.2f, R(0, 360), 0.3f, 0.8f);
        }
        Hangings(f, 2);
        Elite(f, 0, -0.8f, 180);
        Clutter(f, 0.015f);
        Decay();
    }

    /// <summary>
    /// A throne room: the dais with colonnades, armour stands and banners; or a war room with a long table and weapon
    /// stands before the dais; or the dais among fallen columns and broken statues.
    /// </summary>
    private void Throne(Frame f)
    {
        Dais(f, f.HalfD - 2);
        int variant = _rnd.Next(3);
        switch (variant)
        {
            case 0:
                if (Chance(0.5))
                {
                    Loft(f);
                }
                if (f.HalfA >= 5)
                {
                    for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
                    {
                        Pillar(f, -(f.HalfA - 2.4f), v);
                        Pillar(f, f.HalfA - 2.4f, v);
                    }
                }
                Runner(f, -f.HalfD + 1.2f, f.HalfD - 6.2f);
                for (float v = -f.HalfD + 4; v <= f.HalfD - 7; v += 4)
                {
                    Put(f, "ArmorStand_Male", -(f.HalfA - 0.8f), v, 90, 0.6f);
                    Put(f, "ArmorStand_Male", f.HalfA - 0.8f, v, -90, 0.6f);
                }
                break;
            case 1:
                _o.Add("piece_table_oak", f.W(0, -1, f.Top + Lift), f.Yaw(90));
                for (float dv = -3f; dv <= 3.01f; dv += 1.5f)
                {
                    f.Take(0, -1 + dv, 0.8f);
                }
                Put(f, "piece_cartographytable", 0, -1, 0, 0, y: 0.82f, force: true);
                foreach (int side in new[] { -1, 1 })
                {
                    for (float dv = -2.4f; dv <= 2.41f; dv += 1.6f)
                    {
                        Put(f, "piece_chair03", side * 1.4f, -1 + dv, side < 0 ? 90 : -90, 0.4f);
                    }
                }
                foreach (var sp in f.WallSpots(4, 3).Where(x => x.Yaw is 90 or -90).OrderBy(_ => _rnd.Next()).Take(4))
                {
                    var (u, v) = In(sp, 0.7f);
                    Put(f, One("Morkhalla_WeaponStand", "ArmorStand_Male"), u, v, sp.Yaw, 0.7f);
                }
                Pilasters(f, 8);
                break;
            default:
                for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        if (Chance(0.5))
                        {
                            Column(f, side * (f.HalfA - 2.4f), v);
                        }
                        else if (_mat.Column[0].Prefab == "stone_pillar")
                        {
                            var q = DungeonOut.Yaw(f.Yaw(R(60, 120))) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
                            _o.Add("stone_pillar", f.W(side * (f.HalfA - 3.4f), v, f.F + 0.55f), q);
                            f.Take(side * (f.HalfA - 3.4f), v, 1.4f);
                        }
                    }
                }
                Put(f, "StatueThor_broken_bottom", -3, f.HalfD - 7, R(0, 360), 1f);
                Put(f, "StatueFreya_broken_right", 3, -f.HalfD + 3, R(0, 360), 1f);
                Clutter(f, 0.02f, "Morkhalla_Rubble2", "Morkhalla_Rubble1", "stone_wall_1x1_ruin");
                Webs(f, 0.6f);
                break;
        }
        Hangings(f, 4, "piece_banner01", "piece_banner02", "piece_banner05", "piece_banner06", "piece_banner09", "piece_banner10");
        Trophies(f, 2);
        WallGems(f, 2);
        Clusters(f, 1, "armoury");
        Lights(important: true);
        Elite(f, -1.8f, f.HalfD - 6.5f, 180);
        Elite(f, 1.8f, f.HalfD - 6.5f, 180);
        Foes(f, 0.6f);
        Decay();
    }

    /// <summary>n trophies on item stands on the walls.</summary>
    private void Trophies(Frame f, int n)
    {
        string[] options = [ "prop_itemstand_TrophyDraugrElite", "prop_itemstand_TrophyGoblinBrute", "prop_itemstand_TrophyGoblinShaman", "prop_itemstand_TrophyGreydwarf",
            "prop_itemstand_TrophyGreydwarfBrute", "prop_itemstand_TrophySeekerBrute" ];
        foreach (var s in f.WallSpots(3, 3).OrderBy(_ => _rnd.Next()).Take(n))
        {
            var (u, v) = In(s, 0.06f);
            _o.Add(One(options), f.W(u, v, f.Top + 2.1f), f.Yaw(s.Yaw));
        }
    }

    /// <summary>A bed that suits the material.</summary>
    private string Bed => _mat.Name switch { "Goblin" => "goblin_bed", "Grausten" => "prop_ashwood_bed", _ => "dvergrprops_bed" };

    /// <summary>
    /// Barracks: beds along both walls with a table in the middle; or bedrolls round a fire with a weapon stand; or
    /// bunks in pairs with chests at their feet and the armour by the door.
    /// </summary>
    private void Barracks(Frame f)
    {
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (int side in new[] { -1, 1 })
                {
                    for (float v = -f.HalfD + 1.6f; v <= f.HalfD - 1; v += 1.9f)
                    {
                        float u = side * (f.HalfA - 1.15f);
                        if (f.NearDoor(u, v, 2.4f))
                        {
                            continue;
                        }
                        if (Put(f, Bed, u, v, (side < 0 ? 90 : -90) + R(-7, 7), 0.75f) && Chance(0.5))
                        {
                            Put(f, "dvergrprops_crate", side * (f.HalfA - 2.7f), v, 0, 0.5f);
                        }
                    }
                }
                Put(f, "dvergrprops_table", 0, 0, 0, 1.1f);
                foreach (var (u, v) in new[] { (-1.2f, 0f), (1.2f, 0f), (0f, -1f) })
                {
                    Put(f, "dvergrprops_stool", u, v, 0, 0.3f);
                }
                Put(f, "prop_Tankard", 0.3f, 0.1f, R(0, 360), 0, y: 0.78f, force: true);
                Put(f, "ArmorStand_Male", 0, f.HalfD - 0.7f, 180, 0.6f);
                break;
            case 1:
                Put(f, "Morkhalla_firepit", 0, 0, R(0, 360), 1.4f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathF.PI / 3 + MathF.PI / 6;
                    PutNear(f, One("Morkhalla_Bedroll1", "Morkhalla_Bedroll2"), 2.8f * MathF.Cos(a), 2.8f * MathF.Sin(a), a * 180 / MathF.PI + 90, 0.8f, 0.5f);
                }
                Put(f, "Morkhalla_WeaponStand", 0, f.HalfD - 0.8f, 180, 0.8f);
                Put(f, "Morkhalla_Trainingdummy1", -(f.HalfA - 1.2f), f.HalfD - 1.5f, 135, 0.6f);
                Put(f, "prop_wood_stack", f.HalfA - 1.2f, f.HalfD - 1.3f, R(0, 360), 0.8f);
                break;
            default:
                foreach (int side in new[] { -1, 1 })
                {
                    for (float v = -f.HalfD + 2.4f; v <= f.HalfD - 1.5f; v += 3.2f)
                    {
                        float u = side * (f.HalfA - 1.15f);
                        if (f.NearDoor(u, v, 2.4f))
                        {
                            continue;
                        }
                        Put(f, Bed, u, v - 0.5f, side < 0 ? 90 : -90, 0.75f);
                        Put(f, Bed, u, v + 0.6f, side < 0 ? 90 : -90, 0.75f);
                        Put(f, "piece_chest_wood", side * (f.HalfA - 2.6f), v, side < 0 ? 90 : -90, 0.5f);
                    }
                }
                foreach (var m in f.Mouths.Take(1))
                {
                    var (mu, mv) = f.Local(m.X, m.Z);
                    var (su, sv) = (m.In.X * f.A.X + m.In.Z * f.A.Z, m.In.X * f.D.X + m.In.Z * f.D.Z);
                    Put(f, "ArmorStand_Male", mu + su * 1.2f - sv * 2.6f, mv + sv * 1.2f + su * 2.6f, 0, 0.6f);
                    Put(f, "ArmorStand_Female", mu + su * 1.2f + sv * 2.6f, mv + sv * 1.2f - su * 2.6f, 0, 0.6f);
                }
                Put(f, "piece_logbench01", 0, 0, 90, 0.6f);
                break;
        }
        Hangings(f, 1, Drapes);
        Clusters(f, 2, "stores", "armoury", "camp");
        Lights();
        Foes(f, 1.5f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>An armoury: armour stands along the walls with a work table; or weapon stands in rows with a target and a dummy. Trophies, a chest.</summary>
    private void Armory(Frame f)
    {
        if (Chance(0.5))
        {
            foreach (var s in f.WallSpots(1.6f, 2.8f))
            {
                var (u, v) = In(s, 0.6f);
                Put(f, Chance(0.6) ? "ArmorStand_Male" : "ArmorStand_Female", u, v, s.Yaw, 0.7f);
            }
            Put(f, "piece_table", 0, 0, 0, 1.3f);
            Put(f, "prop_piece_workbench_ext3", 0, 0, 0, 0, y: 0.82f, force: true);
        }
        else
        {
            foreach (var s in f.WallSpots(2.4f, 2.8f))
            {
                var (u, v) = In(s, 0.7f);
                Put(f, Chance(0.7) ? "Morkhalla_WeaponStand" : "ArmorStand_Male", u, v, s.Yaw, 0.8f);
            }
            Put(f, "piece_ArcheryTarget", 0, f.HalfD - 2.2f, 180, 0.7f);
            Put(f, One("Morkhalla_Trainingdummy1", "Morkhalla_Trainingdummy2"), 0, -0.5f, R(0, 360), 0.7f);
        }
        Trophies(f, 2);
        Chest(f, 0, -1.6f, 180);
        Lights();
        Foes(f, 0.6f);
        Decay();
    }

    /// <summary>The feast laid out on tables, the biome's where the game has one.</summary>
    private string FeastFood => _b.Name switch { "Ashlands" => "prop_FeastAshlands", "Deep North" => "PropFeastDeepNorth", _ => "prop_FeastMeadows" };

    /// <summary>
    /// A feast hall: long tables end to end down the middle with benches and the hearth at the end; or the high table
    /// on a dais at the far end with rows of benches before it and a fire in the middle.
    /// </summary>
    private void Feast(Frame f)
    {
        float v0 = -f.HalfD + 3, v1 = f.HalfD - 3.6f;
        if (Chance(0.5))
        {
            for (float v = v0 + 3.3f; v + 3.3f <= v1 + 0.01f; v += 6.8f)
            {
                _o.Add("piece_table_oak", f.W(0, v, f.Top + Lift), f.Yaw(90));
                for (float dv = -3f; dv <= 3.01f; dv += 1.5f)
                {
                    f.Take(0, v + dv, 0.8f);
                }
                foreach (float dv in new[] { -2f, 0f, 2f })
                {
                    _o.Add(Chance(0.7) ? FeastFood : "prop_Tankard", f.W(R(-0.3f, 0.3f), v + dv, f.Top + 0.82f + Lift), f.Yaw(R(0, 360)));
                }
                foreach (int side in new[] { -1, 1 })
                {
                    for (float dv = -2.6f; dv <= 2.61f; dv += 2.6f)
                    {
                        Put(f, "piece_bench01", side * 1.5f, v + dv, side < 0 ? 90 : -90, 0.45f);
                    }
                }
            }
            Put(f, "piece_chair03", 0, v1 + 0.6f, 180, 0.4f);
            Put(f, "prop_hearth", 0, f.HalfD - 1.6f, 180, 1.4f);
        }
        else
        {
            Plinth(f, 0, f.HalfD - 2, 8, 2);
            _o.Add("piece_table_oak", f.W(0, f.HalfD - 2, f.F + 1 + Lift * 2), f.Yaw(0));
            foreach (float du in new[] { -2f, 0f, 2f })
            {
                _o.Add(Chance(0.7) ? FeastFood : "prop_Tankard", f.W(du, f.HalfD - 2, f.F + 1.82f + Lift * 2), f.Yaw(R(0, 360)));
                _o.Add("piece_chair03", f.W(du, f.HalfD - 1.1f, f.F + 1 + Lift * 2), f.Yaw(180));
            }
            Steps(f, 0, f.HalfD - 4, 180, 2);
            Put(f, One("prop_bonfire", "Morkhalla_firepit"), 0, -1, R(0, 360), 1.6f);
            for (float v = -f.HalfD + 2.5f; v <= f.HalfD - 5.5f; v += 2)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    Put(f, "piece_bench01", side * 2.8f, v, 0, 0.45f);
                }
            }
            Trophies(f, 2);
        }
        Hangings(f, 3, Drapes);
        Lights();
        Foes(f, 1.2f);
        Clutter(f, 0.008f);
        Decay();
    }

    /// <summary>A kitchen: hearth and cauldron, preparation and butcher's tables, stores by the walls.</summary>
    private void Kitchen(Frame f)
    {
        if (Chance(0.5))
        {
            // The cook's own stations: a cauldron over its fire, an oven, the fermenter and the mead cauldron, a bucket.
            Put(f, "piece_oven", 0, f.HalfD - 1.1f, 180, 1.6f);
            Put(f, "piece_cauldron", -(f.HalfA - 1.3f), f.HalfD - 1.3f, 135, 0.9f);
            Put(f, "piece_preptable", f.HalfA - 0.5f, 0, -90, 1.3f);
            Put(f, "fermenter", -(f.HalfA - 1.1f), -1, 90, 1.0f);
            Put(f, "piece_MeadCauldron", -(f.HalfA - 0.8f), -2.8f, 90, 0.6f);
            Put(f, "bucket", f.HalfA - 0.6f, -2.4f, R(0, 360), 0.4f);
            Put(f, "coal_pile", 1.2f, f.HalfD - 2.8f, R(0, 360), 0.9f);
            foreach (var s in f.WallSpots(1.4f, 2.6f).OrderBy(_ => _rnd.Next()).Take(3))
            {
                var (su, sv) = In(s, 0.6f);
                Put(f, One("barrell", "piece_chest_barrel", "prop_wood_stack", "CastleKit_braided_box01"), su, sv, R(0, 360), 0.6f);
            }
            Lights();
            Foes(f, 0.5f);
            Clutter(f, 0.01f);
            Decay();
            return;
        }
        Put(f, "prop_hearth", 0, f.HalfD - 1.6f, 180, 1.6f);
        Put(f, "prop_piece_cauldron", 0, f.HalfD - 3.4f, 0, 0.6f);
        Put(f, "prop_preptable", -(f.HalfA - 0.5f), 0, 90, 1.3f);
        Put(f, "prop_cauldron_ext6_rollingpins", -(f.HalfA - 0.5f), 0.6f, 90, 0, y: 1.2f, force: true);
        Put(f, "prop_cauldron_ext3_butchertable", f.HalfA - 1.2f, 0, -90, 0.8f);
        Put(f, "prop_cauldron_ext5_mortarandpestle", f.HalfA - 0.6f, -2.4f, -90, 0.8f);
        Put(f, "prop_piece_cookingstation", -2.4f, f.HalfD - 1.4f, 180, 0.8f);
        foreach (var s in f.WallSpots(1.4f, 2.6f).OrderBy(_ => _rnd.Next()).Take(5))
        {
            var (u, v) = In(s, 0.6f);
            Put(f, One("barrell", "CargoCrate", "prop_wood_stack"), u, v, R(0, 360), 0.6f);
        }
        Lights();
        Foes(f, 0.5f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>
    /// A storeroom: barrels and crates along the walls with shelves; or stacked in a corner block with a cart; or
    /// shelves in rows forming aisles. Loot lying about, maybe a chest.
    /// </summary>
    private void Store(Frame f)
    {
        string[] stock = _mat.Name == "Dvergr" ? ["dvergrprops_barrel", "dvergrprops_crate", "dvergrprops_crate_long"] : ["barrell", "CargoCrate", "barrell", "prop_wood_stack"];
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (var s in f.WallSpots(1.2f, 2.6f))
                {
                    var (u, v) = In(s, 0.6f);
                    if (Chance(0.75))
                    {
                        Put(f, One(stock), u, v, s.Yaw + R(-15, 15), 0.55f);
                    }
                }
                foreach (var s in f.WallSpots(2.4f, 2.6f).OrderBy(_ => _rnd.Next()).Take(2))
                {
                    var (u, v) = In(s, 0.12f);
                    Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.5f);
                }
                break;
            case 1:
                foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex).OrderBy(_ => _rnd.Next()).Take(2))
                {
                    for (float du = 0.6f; du <= 2.5f; du += 1.1f)
                    {
                        for (float dv = 0.6f; dv <= 2.5f; dv += 1.1f)
                        {
                            var (cu, cv) = (u + du * MathF.Sign(MathF.Sin(yaw * MathF.PI / 180) + 0.001f), v + dv * MathF.Sign(MathF.Cos(yaw * MathF.PI / 180) + 0.001f));
                            Put(f, One(stock), cu, cv, R(0, 360), 0.5f);
                        }
                    }
                }
                Put(f, "Cart", R(-1, 1), R(-1, 1), R(0, 360), 1.8f);
                break;
            default:
                for (float u = -f.HalfA + 1.6f; u <= f.HalfA - 1.6f; u += 3.2f)
                {
                    for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 2)
                    {
                        Put(f, Chance(0.7) ? "dvergrprops_shelf" : One(stock), u, v, 90, 0.5f);
                    }
                }
                break;
        }
        Pickables(f, 1);
        if (Chance(0.5))
        {
            ChestAgainstWall(f);
        }
        Webs(f, 0.3f);
        Lights();
        Foes(f, 0.3f);
        Decay();
    }

    /// <summary>A forge: forge and workbench pieces, wood, and metal bars when there is loot.</summary>
    private void Forge(Frame f)
    {
        if (Chance(0.5))
        {
            // The smith's own: the forge with its bellows, a smelter or the biome's furnace, coal and ore, bars.
            string furnace = _b.Name switch { "Ashlands" => "blackforge", "Mistlands" => "eitrrefinery", "Deep North" => "piece_FrostFoundry", _ => "smelter" };
            Put(f, "forge", 0, f.HalfD - 1.2f, 180, 1.2f);
            Put(f, "prop_forge_ext2", -1.4f, f.HalfD - 1.1f, 180, 0.5f);
            Put(f, furnace, -(f.HalfA - 1.8f), -(f.HalfD - 1.8f), 45, 1.8f);
            Put(f, "piece_stonecutter", f.HalfA - 1.0f, 0.4f, -90, 1.0f);
            Put(f, "coal_pile", f.HalfA - 1.2f, -(f.HalfD - 1.2f), R(0, 360), 0.9f);
            Put(f, One("stone_pile", "coal_pile", "prop_wood_stack"), -(f.HalfA - 1.0f), 1.6f, R(0, 360), 0.9f);
            if (_s.Loot > 0)
            {
                Put(f, _b.Bars, 1.6f, f.HalfD - 2.8f, R(-10, 10), 0.5f);
            }
            Put(f, "bucket", 1.2f, -0.4f, R(0, 360), 0.4f);
            Lights();
            Foes(f, 0.5f);
            Clutter(f, 0.01f);
            Decay();
            return;
        }
        Put(f, "prop_forge_ext2", 0, 0.6f, 0, 0.6f);
        Put(f, "prop_forge_ext5", 1.4f, 0.6f, 0, 0.6f);
        Brazier(f, -1.4f, 0.6f);
        Put(f, "prop_piece_workbench_ext2", 0, f.HalfD - 0.4f, 180, 1.2f);
        Put(f, "prop_piece_workbench_ext1", -(f.HalfA - 0.5f), 1.6f, 90, 0.7f);
        Put(f, "prop_piece_workbench_ext4", f.HalfA - 0.4f, 1.6f, -90, 0.7f);
        Put(f, "prop_wood_stack", -(f.HalfA - 1.4f), -(f.HalfD - 1.4f), R(0, 360), 1.2f);
        if (_s.Loot > 0)
        {
            Put(f, _b.Bars, f.HalfA - 1.2f, -(f.HalfD - 1.2f), R(0, 360), 0.5f);
        }
        Lights();
        Foes(f, 0.5f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>
    /// A study: shelves along the walls round a table; or shelves in rows forming aisles with a desk at the end; or a
    /// scriptorium of several desks with chairs and lanterns. A chest.
    /// </summary>
    private void Library(Frame f)
    {
        switch (_rnd.Next(3))
        {
            case 0:
                foreach (var s in f.WallSpots(2, 2.8f))
                {
                    var (u, v) = In(s, 0.12f);
                    Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.6f);
                }
                Put(f, One("rug_wolf", "rug_fur", "rug_deer"), 0, 0, 0, 0, force: true);
                Put(f, "dvergrprops_table", 0, 0, 0, 1.2f);
                Put(f, "dvergrprops_chair", 0, -1.1f, 0, 0.4f);
                Put(f, "dvergrprops_chair", 0, 1.1f, 180, 0.4f);
                Put(f, "dvergrprops_lantern_standing", 0.5f, 0, 0, 0, y: 0.78f, force: true);
                Chest(f, 0, f.HalfD - 2.2f, 180);
                break;
            case 1:
                for (float u = -f.HalfA + 1.5f; u <= f.HalfA - 1.5f; u += 3)
                {
                    for (float v = -f.HalfD + 2; v <= f.HalfD - 3.5f; v += 1.1f)
                    {
                        Put(f, "dvergrprops_shelf", u, v, 90, 0.45f);
                    }
                }
                Put(f, "dvergrprops_table", 0, f.HalfD - 1.5f, 90, 1.2f);
                Put(f, "dvergrprops_chair", 0, f.HalfD - 2.6f, 0, 0.4f);
                Put(f, "dvergrprops_lantern_standing", 0.6f, f.HalfD - 1.5f, 0, 0, y: 0.78f, force: true);
                Chest(f, -(f.HalfA - 1), f.HalfD - 1, 180);
                break;
            default:
                foreach (var (u, v) in new[] { (-2.2f, -1.5f), (2.2f, -1.5f), (-2.2f, 1.5f), (2.2f, 1.5f) })
                {
                    if (Put(f, "piece_table", u, v, 0, 1f))
                    {
                        Put(f, "piece_chair", u, v - 1f, 0, 0.3f);
                        Put(f, "piece_hoodedlantern", u + 0.5f, v, 0, 0, y: 0.82f, force: true);
                    }
                }
                foreach (var s in f.WallSpots(2.4f, 2.8f).OrderBy(_ => _rnd.Next()).Take(3))
                {
                    var (u, v) = In(s, 0.12f);
                    Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.6f);
                }
                Chest(f, 0, f.HalfD - 0.9f, 180);
                break;
        }
        Clusters(f, 1, "scribe");
        Lights();
        Foes(f, 0.3f);
        Decay();
    }

    /// <summary>A block of cells either side of an aisle 4 m wide, each 4 m along it behind a grate.</summary>
    private void Cells(Frame f)
    {
        float aisle = 2;
        int n = (int)MathF.Floor((2 * f.HalfD) / 4);
        float v0 = -n * 2f;
        for (int i = 0; i < n; i++)
        {
            float a = v0 + 4 * i, b = a + 4;
            foreach (int side in new[] { -1, 1 })
            {
                float front = side * aisle, back = side * f.HalfA;
                if (i > 0)
                {
                    InnerWall(f, Math.Min(front, back), a, Math.Max(front, back), a, 0, 4);
                }
                InnerWall(f, front, a, front, a + 1, 0, 4);
                InnerWall(f, front, b - 1, front, b, 0, 4);
                InnerWall(f, front, a + 1, front, b - 1, 3, 4);
                _o.Add("iron_grate", f.W(front, a + 2, f.F + 1), f.Yaw(90));
                float mid = (front + back) / 2;
                Put(f, Chance(0.5) ? "goblin_strawpile" : "rug_straw", mid, (a + b) / 2, R(0, 360), 0, force: true);
                Hang(f, "Morkhalla_WallChain1", (back, (a + b) / 2, side < 0 ? 90 : -90));
                if (Chance(0.6))
                {
                    _o.Add(One("Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Skull1"), f.W(mid + side * 0.8f, a + R(1, 3), f.Top + Lift), f.Yaw(R(0, 360)));
                }
                else if (Chance(0.3) && _s.Loot > 0)
                {
                    _o.Add(_b.Pickable, f.W(mid, a + 2, f.Top + Lift), f.Yaw(R(0, 360)));
                }
                else if (Chance(0.4) && _s.Monsters > 0)
                {
                    var p = f.W(mid, a + 2, f.Top + Lift);
                    var foe = One(_foes);
                    _o.Add(_s.Respawn ? foe.Spawner : foe.Creature, p, f.Yaw(side < 0 ? 90 : -90));
                }
            }
        }
        // The aisle's walls are the cells': only the aisle is free. Each row of cells is kept by circles a metre apart
        // along its middle, each just wide enough that together they cover the row up to its grates.
        float half = (f.HalfA - aisle) / 2, keep = MathF.Sqrt(half * half + 0.25f);
        foreach (int side in new[] { -1, 1 })
        {
            for (float v = v0 + 0.5f; v < -v0; v++)
            {
                f.Take(side * (aisle + half), v, keep);
            }
        }
        for (float v = v0 + 2; v < -v0; v += 8)
        {
            Put(f, _b.Torch, aisle - 0.4f, v, -90, 0.3f);
        }
        Foes(f, 0.3f, _b.Elites.Length > 0 ? _b.Elites : null);
    }

    /// <summary>A torture chamber: a butcher's table, chains on the walls, bones about.</summary>
    private void Torture(Frame f)
    {
        Put(f, "prop_cauldron_ext3_butchertable", 0, 0, R(0, 360), 1.1f);
        Brazier(f, 1.8f, 1.2f);
        Put(f, "prop_piece_workbench_ext3", -(f.HalfA - 0.6f), 0, 90, 1f);
        foreach (var s in f.WallSpots(2.4f, 3).OrderBy(_ => _rnd.Next()).Take(4))
        {
            Hang(f, "Morkhalla_WallChain1", s);
        }
        if (f.S.Tall)
        {
            _o.Add("dvergrprops_hooknchain", f.W(-1.5f, -1.5f, f.Top + f.H - 2.5f), f.Yaw(0));
        }
        Clutter(f, 0.04f, "Skull1", "LargeBone", "Pickable_ForestCryptRemains01", "skull_pile");
        Lights();
        Foes(f, 0.7f);
        Decay();
    }

    /// <summary>A guard room: a table with stools and armour stands or barrels; or a drill room with weapon stands, a dummy and a target. An elite guard.</summary>
    private void Guard(Frame f)
    {
        if (Chance(0.5))
        {
            Put(f, "dvergrprops_table", 0, 0, 90, 1f);
            Put(f, "dvergrprops_stool", -1, 0, 90, 0.3f);
            Put(f, "dvergrprops_stool", 1, 0, -90, 0.3f);
            Put(f, "prop_Tankard", 0, 0.2f, 0, 0, y: 0.78f, force: true);
            foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(2))
            {
                var (u, v) = In(s, 0.5f);
                Put(f, Chance(0.5) ? "ArmorStand_Male" : "barrell", u, v, s.Yaw, 0.6f);
            }
        }
        else
        {
            Put(f, "Morkhalla_WeaponStand", 0, f.HalfD - 0.8f, 180, 0.8f);
            Put(f, One("Morkhalla_Trainingdummy1", "Morkhalla_Trainingdummy2"), -1.5f, 0.5f, R(0, 360), 0.6f);
            Put(f, "piece_ArcheryTarget", f.HalfA - 0.8f, 0, -90, 0.6f);
            Put(f, "piece_logbench01", -(f.HalfA - 0.6f), -1, 90, 0.6f);
            Put(f, "ArmorStand_Male", -(f.HalfA - 0.8f), f.HalfD - 1.2f, 135, 0.6f);
        }
        Clusters(f, 1, "meal", "armoury");
        Lights(important: true);
        Elite(f, 0, f.HalfD - 1.6f, 180);
        Foes(f, 1f);
        Decay();
    }

    /// <summary>The treasury: treasure piles and stacks, metal bars, chests, braziers in the corners.</summary>
    private void Treasury(Frame f)
    {
        float loot = Math.Max(0.5f, _s.Loot) * Depth(f);
        for (int i = 0; i < Count(1 + loot); i++)
        {
            PutNear(f, "treasure_pile", R(-1.5f, 1.5f), R(-0.5f, 1.5f), R(0, 360), 1.2f, 1.5f);
        }
        for (int i = 0; i < Count(3 * loot); i++)
        {
            PutNear(f, "treasure_stack", R(-2, 2), R(-1, 2), R(0, 360), 0.3f, 2f);
        }
        for (int i = 0; i < Count(2 * loot); i++)
        {
            PutNear(f, _b.Bars, R(-2.5f, 2.5f), f.HalfD - 1.2f, R(-10, 10), 0.5f, 1f);
        }
        foreach (var s in f.WallSpots(2.5f, 3).Where(s => s.Yaw != 0).OrderBy(_ => _rnd.Next()).Take(Count(1.5f + loot)))
        {
            var (u, v) = In(s, 0.6f);
            Chest(f, u, v, s.Yaw, force: true);
        }
        Cores(f, Count(1 + loot));
        WallGems(f, Count(1 + loot));
        foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex))
        {
            var (bu, bv) = In((u, v, yaw), 1f);
            Brazier(f, bu, bv);
        }
        Pickables(f, 2);
    }

    /// <summary>
    /// The arena, one of several: a ring of columns and braziers round a great rug with the boss upon it; the boss
    /// enthroned on a dais at the far end over heaps of skulls, braziers in rows before it; a crypt arena of columns
    /// on a grid, sarcophagi between them, chains above. Its followers about it.
    /// </summary>
    private void Boss(Frame f)
    {
        float r = Math.Min(f.HalfA, f.HalfD) - 3.5f;
        int variant = _rnd.Next(_mat.Name is "Stone" or "Grausten" or "Marble" ? 4 : 3);
        switch (variant)
        {
            case 3:
                // The pit: ledges round a sunken floor, columns at its corners, the master down in the middle.
                Ledges(f);
                foreach (var (cu, cv) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    Pillar(f, cu * (f.S.Across - 3), cv * (f.S.Deep - 3));
                }
                if (f.S.Tall)
                {
                    _o.Add("StatueEvil", f.W(0, f.HalfD - 1.1f, f.F + 1 + Lift * 2 + DungeonProps.Foot("StatueEvil")), f.Yaw(180));
                }
                break;
            case 0:
                // A ring of columns, braziers between, the master in the middle.
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.PI / 4 + MathF.PI / 8;
                    float u = r * MathF.Cos(a), v = r * MathF.Sin(a);
                    if (!f.NearDoor(u, v, 3.5f))
                    {
                        Column(f, u, v);
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    float a = i * MathF.PI / 2 + MathF.PI / 4;
                    Brazier(f, (r - 2.2f) * MathF.Cos(a), (r - 2.2f) * MathF.Sin(a));
                }
                break;
            case 1:
                // The dais at the far end, the boss's seat on it, rows of braziers and skulls before it.
                Plinth(f, 0, f.HalfD - 3, 8, 4);
                Steps(f, 0, f.HalfD - 6, 180, 4);
                _o.Add(ThronePiece, f.W(0, f.HalfD - 2.2f, f.F + 1 + Lift * 2), f.Yaw(180 + DungeonProps.Turn(ThronePiece)));
                _o.Add("skull_pile", f.W(-2.6f, f.HalfD - 2.6f, f.F + 1 + Lift * 2), f.Yaw(R(0, 360)));
                _o.Add("skull_pile", f.W(2.6f, f.HalfD - 2.6f, f.F + 1 + Lift * 2), f.Yaw(R(0, 360)));
                for (float v = -f.HalfD + 3; v <= f.HalfD - 7; v += 4)
                {
                    Brazier(f, -(f.HalfA - 1.6f), v);
                    Brazier(f, f.HalfA - 1.6f, v);
                    Put(f, "skull_pile", -(f.HalfA - 3.2f), v + 2, R(0, 360), 0.5f);
                    Put(f, "skull_pile", f.HalfA - 3.2f, v + 2, R(0, 360), 0.5f);
                }
                Pilasters(f, 4);
                break;
            default:
                // Columns on a grid, sarcophagi between them, chains and cobwebs.
                for (float u = -f.HalfA + 3; u <= f.HalfA - 3; u += 4)
                {
                    for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
                    {
                        if (Math.Abs(u) < 2.5f && Math.Abs(v) < 2.5f || f.NearDoor(u, v, 3))
                        {
                            continue;
                        }
                        if (MathF.Abs(u) >= f.HalfA - 3.5f || MathF.Abs(v) >= f.HalfD - 3.5f)
                        {
                            Column(f, u, v);
                        }
                        else
                        {
                            Put(f, "crypt_skeleton_chest", u, v, R(0, 360), 1.2f);
                        }
                    }
                }
                Chains(f, 4);
                Webs(f, 0.6f);
                break;
        }
        if (f.S.Tall)
        {
            if (variant is not (1 or 3))
            {
                Put(f, "StatueEvil", 0, f.HalfD - 1.6f, 180, 1.6f);
            }
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add(_b.Name == "Ashlands" ? "CharredBanner1" : "cloth_hanging_long", f.W(side * 3.4f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
            _o.Add("Morkhalla_StatueSword_hanging", f.W(0, f.HalfD - 5, f.Top + f.H - 2.7f), f.Yaw(0));
        }
        else if (variant != 1)
        {
            Put(f, One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom"), 0, f.HalfD - 1.6f, 180, 1.5f);
        }
        // A great rug in the middle, the boss upon it; a giant's sword before the altar; banners.
        foreach (var (cu, cv, yaw) in new[] { (-2.18f, -2.18f, 0f), (2.18f, -2.18f, 90f), (2.18f, 2.18f, 180f), (-2.18f, 2.18f, 270f) })
        {
            _o.Add("Morkhalla_Rug_corner", f.W(cu, cv, f.Top + 0.06f), f.Yaw(yaw));
        }
        if (f.HalfA >= 10 && variant != 1)
        {
            _o.Add("Morkhalla_StatueSword", f.W(0, f.HalfD - 4.4f, f.Top + 0.3f), f.Yaw(R(-8, 8)));
            f.Take(-3, f.HalfD - 4.4f, 1.2f);
            f.Take(3, f.HalfD - 4.4f, 1.2f);
        }
        Hangings(f, 4, Drapes);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI / 2;
            PutNear(f, "skull_pile", (r + 1.4f) * MathF.Cos(a), (r + 1.4f) * MathF.Sin(a), R(0, 360), 0.6f, 1f);
        }
        f.Take(0, 0, 1.5f);
        if (_boss != null)
        {
            Spawn(f, _boss, 0, 0.5f, 180, Math.Clamp(1 + (int)MathF.Round(_s.Monsters), 1, 3), once: true, force: true);
        }
        if (_s.Monsters > 0)
        {
            for (int i = 0; i < Count(2 * _s.Monsters); i++)
            {
                if (Spot(f, 0.7f, 2f) is var (u, v))
                {
                    Spawn(f, One(_foes), u, v, 180, 1, once: true);
                }
            }
        }
        Clutter(f, 0.012f, "Skull1", "LargeBone", "lox_ribs", "skull_pile");
        Lights(important: true);
    }

    /// <summary>The antechamber: braziers either side of the boss's gate, a warning sign at the key gate, elite guards.</summary>
    private void Antechamber(Frame f)
    {
        // The way to the boss: braziers either side of its gate, guards, a warning.
        foreach (var m in f.Mouths.Where(m => m.Door.Kind is Door.Key or Door.Grand))
        {
            var (u, v) = f.Local(m.X, m.Z);
            var (sideU, sideV) = (m.In.X * f.A.X + m.In.Z * f.A.Z, m.In.X * f.D.X + m.In.Z * f.D.Z);
            var (alongU, alongV) = (sideV, -sideU);
            Brazier(f, u + sideU * 1.2f + alongU * 3.2f, v + sideV * 1.2f + alongV * 3.2f);
            Brazier(f, u + sideU * 1.2f - alongU * 3.2f, v + sideV * 1.2f - alongV * 3.2f);
            if (m.Door.Kind == Door.Key)
            {
                // The doorway's middle is on the wall's centre line: the sign goes on its inner face, a touch out.
                float yaw = MathF.Atan2(sideU, sideV) * 180 / MathF.PI, inset = _mat.Thick / 2 + 0.04f;
                _o.Add("sign", f.W(u + alongU * 2.6f + sideU * inset, v + alongV * 2.6f + sideV * inset, f.Top + 2.1f), f.Yaw(yaw + DungeonProps.Turn("sign")),
                    [("strings", "text", "Sealed. The key lies within these halls.")]);
            }
        }
        Hangings(f, 2, Drapes);
        Clutter(f, 0.02f, "Skull1", "skull_pile", "LargeBone");
        Elite(f, -1.6f, 0, 180);
        Elite(f, 1.6f, 0, 180);
        Lights(important: true);
    }

    /// <summary>A goblin den: straw and beds by the walls round a trash pile and a totem; or a fire in the middle with the straw round it.</summary>
    private void Den(Frame f)
    {
        if (Chance(0.5))
        {
            foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(4))
            {
                var (u, v) = In(s, 1.1f);
                Put(f, Chance(0.5) ? "goblin_strawpile" : "goblin_bed", u, v, s.Yaw, 1.1f);
            }
            Put(f, "goblin_trashpile", R(-1, 1), R(-1, 1), R(0, 360), 1.6f);
            Brazier(f, R(-2, 2), -1.5f);
        }
        else
        {
            Put(f, "prop_bonfire", 0, 0, R(0, 360), 1.5f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * MathF.PI / 3 + MathF.PI / 6;
                PutNear(f, One("goblin_strawpile", "goblin_bed", "rug_straw"), 2.9f * MathF.Cos(a), 2.9f * MathF.Sin(a), a * 180 / MathF.PI + 90, 0.9f, 0.5f);
            }
            Put(f, "goblin_trashpile", -(f.HalfA - 1.4f), f.HalfD - 1.4f, R(0, 360), 1.4f);
            Put(f, "lox_ribs", f.HalfA - 1.4f, -(f.HalfD - 1.4f), R(0, 360), 0.8f);
        }
        if (!(Chance(0.4) && PutHero(f, 0, f.HalfD - 2, 180, 1.8f)))
        {
            Put(f, "goblin_totempole", 0, f.HalfD - 0.6f, 180, 0.6f);
        }
        Put(f, "goblin_banner", -(f.HalfA - 0.6f), f.HalfD - 1.2f, 90, 0.6f);
        Clusters(f, 2, "camp", "bones");
        Lights();
        Foes(f, 1.5f);
        Clutter(f, 0.025f);
        Decay();
    }

    /// <summary>A totem hall: a brazier ringed by totem poles; or a row of totems along the far wall over a bone-strewn floor; skull piles, shamans if there are any.</summary>
    private void Totem(Frame f)
    {
        if (Chance(0.5))
        {
            Brazier(f, 0, 0);
            for (int i = 0; i < 5; i++)
            {
                float a = i * MathF.PI * 2 / 5;
                Put(f, "goblin_totempole", 3.2f * MathF.Cos(a), 3.2f * MathF.Sin(a), a * 180 / MathF.PI + 90, 0.6f);
            }
        }
        else
        {
            for (float u = -f.HalfA + 1.5f; u <= f.HalfA - 1.5f; u += 2)
            {
                Put(f, "goblin_totempole", u, f.HalfD - 0.8f, 180, 0.6f);
            }
            Brazier(f, -2, f.HalfD - 3);
            Brazier(f, 2, f.HalfD - 3);
            if (!PutHero(f, 0, f.HalfD - 3.4f, 180, 1.6f))
            {
                Put(f, "Goblin_Gem", 0, f.HalfD - 3.4f, 180, 1.2f);
            }
            Clutter(f, 0.03f, "Skull1", "LargeBone", "lox_ribs");
        }
        foreach (var s in f.WallSpots(3, 3).OrderBy(_ => _rnd.Next()).Take(3))
        {
            var (u, v) = In(s, 0.5f);
            Put(f, "skull_pile", u, v, R(0, 360), 0.6f);
        }
        Clusters(f, 1, "bones", "altar");
        Hangings(f, 2);
        Foes(f, 1f, [.. _foes.Where(x => x.Creature.Contains("Shaman", StringComparison.Ordinal)).DefaultIfEmpty(_foes[0])]);
        Foes(f, 0.6f);
        Clutter(f, 0.02f);
        Decay();
    }

    /// <summary>A mine gallery: timber frames every 6 m, crates and tools by the walls, loot lying about.</summary>
    private void Mine(Frame f)
    {
        // Timber frames across the gallery, every 6 m.
        for (float v = -f.HalfD + 3; v <= f.HalfD - 2; v += 6)
        {
            _o.Add("dvergrtown_wood_support", f.W(0, v, f.F), f.Yaw(0));
            f.Take(-(f.HalfA - 0.4f), v, 0.6f);
            f.Take(f.HalfA - 0.4f, v, 0.6f);
        }
        foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(6))
        {
            var (u, v) = In(s, 0.7f);
            Put(f, One("dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_pickaxe", "dvergrprops_crate_long"), u, v, s.Yaw + R(-20, 20), 0.6f);
        }
        Pickables(f, 1.5f);
        Clusters(f, 1, "stores");
        Lights();
        Foes(f, 0.8f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>A workshop: work tables with stools and a lantern, workbench pieces, shelves.</summary>
    private void Workshop(Frame f)
    {
        if (Chance(0.5))
        {
            // The crafter's own stations: a workbench with its tools, the artisan's table, a spinning wheel, a mage's table in the mists.
            Put(f, "piece_workbench", 0, f.HalfD - 0.8f, 180, 1.3f);
            Put(f, "prop_piece_workbench_ext1", -2.0f, f.HalfD - 0.5f, 180, 0.6f);
            Put(f, "prop_piece_workbench_ext3", 2.0f, f.HalfD - 0.5f, 180, 0.6f);
            Put(f, _b.Name == "Mistlands" ? "piece_magetable" : "piece_artisanstation", -(f.HalfA - 1.4f), -0.4f, 90, 1.4f);
            Put(f, "piece_spinningwheel", f.HalfA - 1.2f, -0.6f, -90, 1.1f);
            Put(f, "UpgradeStation", 0, -(f.HalfD - 1.0f), 0, 0.8f);
            Clutter(f, 0.02f, "dvergrprops_crate", "barrell", "prop_wood_stack", "piece_chest_barrel");
            Clusters(f, 1, "stores");
            Lights();
            Foes(f, 0.6f);
            Decay();
            return;
        }
        Put(f, "dvergrprops_table", -1.4f, 0, 90, 1.1f);
        Put(f, "dvergrprops_table", 1.4f, 0, 90, 1.1f);
        Put(f, "dvergrprops_stool", -1.4f, -1.2f, 0, 0.3f);
        Put(f, "dvergrprops_stool", 1.4f, 1.2f, 180, 0.3f);
        Put(f, "dvergrprops_lantern_standing", -1.4f, 0.3f, 0, 0, y: 0.78f, force: true);
        Put(f, "prop_piece_workbench_ext1", -(f.HalfA - 0.5f), 2, 90, 0.7f);
        Put(f, "prop_piece_workbench_ext2", 0, f.HalfD - 0.4f, 180, 1.2f);
        foreach (var s in f.WallSpots(2.2f, 2.8f).OrderBy(_ => _rnd.Next()).Take(3))
        {
            var (u, v) = In(s, 0.12f);
            Put(f, "dvergrprops_shelf", u, v, s.Yaw, 0.6f);
        }
        Clutter(f, 0.02f, "dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_pickaxe");
        Clusters(f, 1, "workshop", "stores");
        Lights();
        Foes(f, 0.6f);
        Decay();
    }

    /// <summary>Quarters: two beds with a wardrobe and a table; or one bed in a corner, a rug, a bath and a chest.</summary>
    private void Quarters(Frame f)
    {
        if (Chance(0.5))
        {
            Put(f, Bed, -(f.HalfA - 1.2f), f.HalfD - 1.6f, 90, 1f);
            Put(f, Bed, f.HalfA - 1.2f, f.HalfD - 1.6f, -90, 1f);
            Put(f, "prop_chest_warderobe", 0, f.HalfD - 0.6f, 180, 0.9f);
            Put(f, One("rug_wolf", "rug_fur", "rug_deer"), 0, -0.5f, 0, 0, force: true);
            Put(f, "dvergrprops_table", 0, -0.5f, 0, 1.1f);
            Put(f, "dvergrprops_chair", -1.1f, -0.5f, 90, 0.4f);
            Put(f, "dvergrprops_chair", 1.1f, -0.5f, -90, 0.4f);
        }
        else
        {
            Put(f, Bed, -(f.HalfA - 1.2f), f.HalfD - 1.2f, 90, 1f);
            Put(f, "piece_chest_wood", -(f.HalfA - 1.2f), f.HalfD - 3.1f, 90, 0.5f);
            Put(f, One("jute_carpet", "jute_carpet_blue", "rug_fur"), 0.8f, 0, 90, 0, force: true);
            Put(f, "piece_table_round", 1.2f, -0.5f, 0, 0.9f);
            Put(f, "piece_chair", 1.2f, -1.6f, 0, 0.3f);
            Put(f, "piece_hoodedlantern", 1.2f, -0.5f, 0, 0, y: 0.82f, force: true);
            Put(f, "prop_chest_warderobe", f.HalfA - 0.7f, f.HalfD - 1.2f, -90, 0.9f);
            if (_mat.Name is not ("Goblin" or "Dvergr"))
            {
                Put(f, "piece_bathtub", f.HalfA - 1.6f, -(f.HalfD - 2), -90, 1.6f);
            }
        }
        Trophies(f, 1);
        if (Chance(0.5))
        {
            ChestAgainstWall(f);
        }
        Clusters(f, 1, "scribe", "camp");
        Lights();
        Foes(f, 0.6f);
        Clutter(f, 0.01f);
        Decay();
    }

    /// <summary>A hidden cache: two chests (whatever the Loot), treasure, bars, a brazier.</summary>
    private void Secret(Frame f)
    {
        Chest(f, -1.4f, f.HalfD - 1, 180, force: true);
        Chest(f, 1.4f, f.HalfD - 1, 180, force: true);
        Put(f, "treasure_pile", 0, 0.4f, R(0, 360), 1.1f);
        Put(f, _b.Bars, -2, -1, R(0, 360), 0.5f);
        Put(f, "Pickable_ForestCryptRemains01", 1.6f, -1.2f, R(0, 360), 0.5f);
        Pickables(f, 1.5f);
        Brazier(f, 2.4f, 1.2f);
    }
}

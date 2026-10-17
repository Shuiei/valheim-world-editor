using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- A room's frame.
    /// <summary>
    /// A space's own frame (u across, right of the way in; v deep, 0 the middle of its own box, recesses left out)
    /// and what is already taken in it. Its tiles are (a, b) of the own box, a across and b deep; a recess's tiles
    /// may lie beyond it, at -1 or one past the last.
    /// </summary>
    internal sealed class Frame
    {
        /// <summary>The space.</summary>
        public readonly DungeonPlan.Space S;
        /// <summary>
        /// Its floor (the marble's top), the top of its floor finish, its wall height, its own box's middle (world x,
        /// z) and the half width and half depth to that box's walls' inner faces (metres).
        /// </summary>
        public readonly float F, Top, H, Cx, Cz, HalfA, HalfD;
        /// <summary>Its own box's tiles across and deep (its recesses not counted).</summary>
        public readonly int Across, Deep;
        /// <summary>The first and last tile across (a) and deep (b) that its tiles reach, recesses included.</summary>
        public readonly int A0, A1, B0, B1;
        /// <summary>The way in (deep, +v) and the way across (+u, to its right), in world axes.</summary>
        public readonly (int X, int Z) D, A;
        /// <summary>Circles kept free (world x, z, radius in metres).</summary>
        public readonly List<(float X, float Z, float R)> Taken = [];
        /// <summary>Its doorways: their middle (world x, z), the way into the room from them, the doorway.</summary>
        public readonly List<(float X, float Z, (int X, int Z) In, DungeonPlan.Doorway Door)> Mouths = [];

        /// <summary>The level the space is on: what lies beyond its walls.</summary>
        private readonly DungeonPlan.Level _level;
        /// <summary>Its own box (tiles, world axes).</summary>
        private readonly (int X0, int Z0, int W, int H) _own;

        /// <summary>The frame of a space, with the floor before each doorway kept free.</summary>
        public Frame(DungeonPlan.Space s, Material mat, DungeonPlan.Level level)
        {
            S = s;
            _level = level;
            F = DungeonPlan.FloorY(s.Level);
            Top = F + (s.IsRoom && s.Room is not (Room.StairTop or Room.StairBottom) ? s.Floor != null ? s.FloorTop : mat.Floor != null ? mat.FloorTop : 0 : 0);
            H = DungeonPlan.HeightOf(s);
            _own = s.Own;
            Cx = (_own.X0 + _own.W / 2f) * 2;
            Cz = (_own.Z0 + _own.H / 2f) * 2;
            D = s.Dir;
            A = (D.Z, -D.X);
            (Across, Deep) = D.X != 0 ? (_own.H, _own.W) : (_own.W, _own.H);
            HalfA = Across - mat.Thick / 2;
            HalfD = Deep - mat.Thick / 2;
            (A0, A1, B0, B1) = (0, Across - 1, 0, Deep - 1);
            foreach (var (tiles, _) in s.Recesses)
            {
                foreach (var t in tiles)
                {
                    var (a, b) = CellOf(t);
                    (A0, A1, B0, B1) = (Math.Min(A0, a), Math.Max(A1, a), Math.Min(B0, b), Math.Max(B1, b));
                }
            }
            foreach (var d in s.Doors)
            {
                var (x, z) = d.Center;
                var inward = d.A == s ? (-d.Dir.X, -d.Dir.Z) : d.Dir;
                Mouths.Add((x, z, inward, d));
                Taken.Add((x + inward.Item1 * 1.6f, z + inward.Item2 * 1.6f, 2.2f));
            }
        }

        /// <summary>Its floor area inside the walls (square metres): its tiles' less the walls' share.</summary>
        public float Area => S.Tiles.Count * 4f * (HalfA * HalfD) / (Across * Deep);

        /// <summary>The world tile at (a across, b deep) of its own box; a and b may lie beyond it, as a recess's do.</summary>
        public (int, int) TileAt(int a, int b) => D switch
        {
            (0, 1) => (_own.X0 + a, _own.Z0 + b),
            (0, -1) => (_own.X0 + _own.W - 1 - a, _own.Z0 + _own.H - 1 - b),
            (1, 0) => (_own.X0 + b, _own.Z0 + _own.H - 1 - a),
            _ => (_own.X0 + _own.W - 1 - b, _own.Z0 + a),
        };

        /// <summary>Whether its tile (a across, b deep) is one of the space's own, a recess's included.</summary>
        public bool Has(int a, int b) => S.Has(TileAt(a, b));

        /// <summary>A world tile's place (a across, b deep) in its own box; the inverse of <see cref="TileAt"/>.</summary>
        private (int A, int B) CellOf((int I, int J) t) => D switch
        {
            (0, 1) => (t.I - _own.X0, t.J - _own.Z0),
            (0, -1) => (_own.X0 + _own.W - 1 - t.I, _own.Z0 + _own.H - 1 - t.J),
            (1, 0) => (_own.Z0 + _own.H - 1 - t.J, t.I - _own.X0),
            _ => (t.J - _own.Z0, _own.X0 + _own.W - 1 - t.I),
        };

        /// <summary>Whether a point of the frame is over one of the room's own tiles (not what its shape cuts away; a recess's included).</summary>
        public bool Inside(float u, float v) => Has((int)MathF.Floor((u + Across) / 2), (int)MathF.Floor((v + Deep) / 2));

        /// <summary>A point of the frame (u, v; y as given) in world space.</summary>
        public Vector3 W(float u, float v, float y) => new(Cx + A.X * u + D.X * v, y, Cz + A.Z * u + D.Z * v);

        /// <summary>A world point (x, z) in the frame.</summary>
        public (float U, float V) Local(float x, float z) => ((x - Cx) * A.X + (z - Cz) * A.Z, (x - Cx) * D.X + (z - Cz) * D.Z);

        /// <summary>A turn in the frame (0 faces the far end) as a world turn (degrees).</summary>
        public float Yaw(float local) => DungeonOut.YawOf(D) + local;

        /// <summary>Whether a circle of radius r at (u, v) is inside the walls (over the room's own tiles) and clear of what is taken.</summary>
        public bool Free(float u, float v, float r)
        {
            // Within the walls' inner faces of the box its tiles reach (its own, and out over any recess).
            float thick = Across - HalfA;
            if (u < 2 * A0 - Across + thick + r - 0.15f || u > 2 * (A1 + 1) - Across - thick - r + 0.15f
                || v < 2 * B0 - Deep + thick + r - 0.15f || v > 2 * (B1 + 1) - Deep - thick - r + 0.15f)
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
        public IReadOnlyList<(float U, float V, float Yaw, bool Convex)> Corners() => _corners ??= FindCorners();

        /// <summary>Backs <see cref="Corners"/>: worked out once, the room's tiles never changing while it is dressed.</summary>
        private List<(float U, float V, float Yaw, bool Convex)>? _corners;

        /// <summary>Works out <see cref="Corners"/>.</summary>
        private List<(float U, float V, float Yaw, bool Convex)> FindCorners()
        {
            float thick = Across - HalfA;
            var corners = new List<(float, float, float, bool)>();
            for (int a = A0; a <= A1 + 1; a++)
            {
                for (int b = B0; b <= B1 + 1; b++)
                {
                    // The point at the low corner of tile (a, b): the four tiles round it.
                    bool[] q = [Has(a - 1, b - 1), Has(a, b - 1), Has(a - 1, b), Has(a, b)];
                    int n = q.Count(x => x);
                    if (n is not (1 or 3))
                    {
                        continue;
                    }
                    // Toward the one tile inside, or away from the one missing.
                    int k = Array.IndexOf(q, n == 1);
                    float du = (k % 2 == 0 ? -1 : 1) * (n == 1 ? 1 : -1), dv = (k < 2 ? -1 : 1) * (n == 1 ? 1 : -1);
                    float u = 2 * a - Across + du * thick, v = 2 * b - Deep + dv * thick;
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
            if (Has(a + da, b + db))
            {
                return false;
            }
            // The world tile beyond, through the frame's axes.
            var t = TileAt(a, b);
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
            for (int a = A0; a <= A1; a++)
            {
                for (int b = B0; b <= B1; b++)
                {
                    if (!Has(a, b) || inRecess.Contains(TileAt(a, b)))
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
                        nooks.Add((2 * a + 1 - Across, 2 * b + 1 - Deep, MathF.Atan2(open[0].Item1, open[0].Item2) * 180 / MathF.PI, 1));
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
        public IReadOnlyList<(float U, float V, float DU, float DV, float Length, float Yaw)> Walls() => _walls ??= FindWalls();

        /// <summary>Backs <see cref="Walls"/>: worked out once, the room and its level never changing while it is dressed.</summary>
        private List<(float U, float V, float DU, float DV, float Length, float Yaw)>? _walls;

        /// <summary>Works out <see cref="Walls"/>.</summary>
        private List<(float U, float V, float DU, float DV, float Length, float Yaw)> FindWalls()
        {
            float thick = Across - HalfA;
            // Each tile's walled sides, in the frame: (along the run, across the run) with the run's start.
            var segments = new List<(float Yaw, float Line, float At)>();
            for (int a = A0; a <= A1; a++)
            {
                for (int b = B0; b <= B1; b++)
                {
                    if (!Has(a, b))
                    {
                        continue;
                    }
                    float u = 2 * a - Across, v = 2 * b - Deep; // the tile's low corner
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
}

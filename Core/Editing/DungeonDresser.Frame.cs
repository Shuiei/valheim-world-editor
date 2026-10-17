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
    internal sealed partial class Frame
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

    }
}

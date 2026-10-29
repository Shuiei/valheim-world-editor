namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    internal sealed partial class Frame
    {
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
    }
}

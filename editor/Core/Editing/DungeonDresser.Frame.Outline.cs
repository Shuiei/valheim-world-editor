namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    internal sealed partial class Frame
    {
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
    }
}

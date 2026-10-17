namespace TerrainEditor.Editing;

public static partial class Uplift
{
    internal sealed partial class Block
    {
        /// <summary>
        /// One pass of a square dilation: each point is on in <paramref name="to"/> when a point within r of it on its
        /// row (or column) is on in <paramref name="from"/>. Tiles missing on the way count as off.
        /// </summary>
        /// <param name="from">The points that are on.</param>
        /// <param name="to">Gets the points within r of them.</param>
        /// <param name="r">How far (points).</param>
        /// <param name="columns">Along the columns (z) rather than the rows (x).</param>
        public void Sweep(bool[] from, bool[] to, int r, bool columns)
        {
            int lines = columns ? _tw : _th;
            for (int a = 0; a < lines; a++)
            {
                for (int o = 0; o < Tile; o++)
                {
                    SweepLine(from, to, r, columns, a, o, forward: true);
                    SweepLine(from, to, r, columns, a, o, forward: false);
                }
            }
        }

        /// <summary>
        /// One direction of a sweep along a single row (or column) of points: a point is marked when the last point on
        /// seen in that direction is within r of it. The forward pass sets each point; the backward pass adds to it.
        /// </summary>
        /// <param name="from">The points that are on.</param>
        /// <param name="to">Gets the points within r of them.</param>
        /// <param name="r">How far (points).</param>
        /// <param name="columns">Along the columns (z) rather than the rows (x).</param>
        /// <param name="a">The row (or column) of tiles.</param>
        /// <param name="o">The offset of the line within each tile.</param>
        /// <param name="forward">Whether the line is walked in increasing order (else decreasing).</param>
        private void SweepLine(bool[] from, bool[] to, int r, bool columns, int a, int o, bool forward)
        {
            int along = columns ? _th : _tw;
            int last = forward ? int.MinValue / 2 : int.MaxValue / 2;
            for (int step = 0; step < along; step++)
            {
                int b = forward ? step : along - 1 - step;
                int s = columns ? _slot[b * _tw + a] : _slot[a * _tw + b];
                if (s < 0)
                {
                    continue;
                }
                for (int j = 0; j < Tile; j++)
                {
                    int i = forward ? j : Tile - 1 - j;
                    int p = s * TilePoints + (columns ? i * Tile + o : o * Tile + i), at = b * Tile + i;
                    if (from[p])
                    {
                        last = at;
                    }
                    if (forward)
                    {
                        to[p] = at - last <= r;
                    }
                    else
                    {
                        to[p] |= last - at <= r;
                    }
                }
            }
        }
    }
}

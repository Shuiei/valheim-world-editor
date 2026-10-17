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
            int lines = columns ? _tw : _th, along = columns ? _th : _tw;
            for (int a = 0; a < lines; a++)
            {
                for (int o = 0; o < Tile; o++)
                {
                    int last = int.MinValue / 2;
                    for (int b = 0; b < along; b++)
                    {
                        int s = columns ? _slot[b * _tw + a] : _slot[a * _tw + b];
                        for (int i = 0; s >= 0 && i < Tile; i++)
                        {
                            int p = s * TilePoints + (columns ? i * Tile + o : o * Tile + i), at = b * Tile + i;
                            if (from[p])
                            {
                                last = at;
                            }
                            to[p] = at - last <= r;
                        }
                    }
                    last = int.MaxValue / 2;
                    for (int b = along - 1; b >= 0; b--)
                    {
                        int s = columns ? _slot[b * _tw + a] : _slot[a * _tw + b];
                        for (int i = Tile - 1; s >= 0 && i >= 0; i--)
                        {
                            int p = s * TilePoints + (columns ? i * Tile + o : o * Tile + i), at = b * Tile + i;
                            if (from[p])
                            {
                                last = at;
                            }
                            to[p] |= last - at <= r;
                        }
                    }
                }
            }
        }
    }
}

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    internal sealed partial class Block
    {
        /// <summary>The place of a point (in points from the first one) in the arrays, or -1 when no tile holds it.</summary>
        private int Slot(int x, int z)
        {
            if (x < 0 || z < 0 || x >= _tw * Tile || z >= _th * Tile)
            {
                return -1;
            }
            int s = _slot[z / Tile * _tw + x / Tile];
            return s < 0 ? -1 : s * TilePoints + z % Tile * Tile + x % Tile;
        }
        /// <summary>The place of a point (in points from the first one) in the arrays, or -1 when it is not in the block's zones.</summary>
        private int Index(int x, int z)
        {
            int p = Slot(x, z);
            return p >= 0 && _inside[p] ? p : -1;
        }

        /// <summary>A point's world position (metres).</summary>
        public (int X, int Z) World(int p)
        {
            var (tx, tz) = _tiles[p / TilePoints];
            int q = p % TilePoints;
            return (X0 + tx * Tile + q % Tile, Z0 + tz * Tile + q / Tile);
        }
        /// <summary>A zone's 65x65 points: (index in the zone, point in the block).</summary>
        public void Each(int zx, int zz, Action<int, int> f)
        {
            int ox = (zx - _zx0) * Tile, oz = (zz - _zz0) * Tile;
            for (int k = 0; k < 65; k++)
            {
                for (int l = 0; l < 65; l++)
                {
                    f(k * 65 + l, Slot(ox + l, oz + k));
                }
            }
        }

        /// <summary>The whole metre nearest a world coordinate, halves rounded up (as Heightmap.WorldToVertex).</summary>
        private static int Nearest(float v) => (int)MathF.Floor(v + 0.5f);
    }
}

using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>
    /// Some zones as one grid of world points, one per metre (zones share their edge points). The points are kept in
    /// tiles of 64x64, one per zone of the block and per zone holding their far edges, so only the zones in the block
    /// take memory however far apart they are; a table of the tiles spans their box (one entry a zone).
    /// </summary>
    internal sealed class Block
    {
        /// <summary>Points a side of a tile: a zone's points without its far edges.</summary>
        private const int Tile = 64;

        /// <summary>Points in a tile.</summary>
        private const int TilePoints = Tile * Tile;

        /// <summary>The lowest zone x and z: the tile table's first tile.</summary>
        private readonly int _zx0, _zz0;

        /// <summary>The tile table's size in tiles (one more than the zones' box each way, for their far edges).</summary>
        private readonly int _tw, _th;

        /// <summary>Whether a tile's zone is in the block, by tile (rows of z).</summary>
        private readonly bool[] _member;

        /// <summary>Each tile's place in the point arrays (in tiles), or -1 when it has none, by tile (rows of z).</summary>
        private readonly int[] _slot;

        /// <summary>The tile (in the table) at each place in the point arrays.</summary>
        private readonly List<(int X, int Z)> _tiles = [];

        /// <summary>Whether a point belongs to a zone of the block (a far-edge tile also holds points of other zones).</summary>
        private readonly bool[] _inside;

        /// <summary>The world position of the first tile's first point (metres).</summary>
        public readonly int X0, Z0;

        /// <summary>The block's zones, by rows (z, then x).</summary>
        public IReadOnlyList<(int X, int Z)> Zones { get; }

        /// <summary>The size of the point arrays (points outside the block's zones included).</summary>
        public int N { get; }

        /// <summary>The points of the block's zones, by rows of the world (z, then x), each once.</summary>
        public int[] Points { get; }

        /// <summary>A block of the given zones.</summary>
        /// <param name="zones">The zones, by rows (z, then x), each once.</param>
        public Block(List<(int X, int Z)> zones)
        {
            Zones = zones;
            _zx0 = zones.Min(z => z.X);
            _zz0 = zones.Min(z => z.Z);
            _tw = zones.Max(z => z.X) - _zx0 + 2;
            _th = zones.Max(z => z.Z) - _zz0 + 2;
            X0 = _zx0 * 64 - 32;
            Z0 = _zz0 * 64 - 32;
            _member = new bool[_tw * _th];
            foreach (var (x, z) in zones)
            {
                _member[(z - _zz0) * _tw + x - _zx0] = true;
            }
            bool Member(int tx, int tz) => tx >= 0 && tz >= 0 && _member[tz * _tw + tx];
            _slot = new int[_tw * _th];
            for (int tz = 0; tz < _th; tz++)
            {
                for (int tx = 0; tx < _tw; tx++)
                {
                    bool used = Member(tx, tz) || Member(tx - 1, tz) || Member(tx, tz - 1) || Member(tx - 1, tz - 1);
                    _slot[tz * _tw + tx] = used ? _tiles.Count : -1;
                    if (used)
                    {
                        _tiles.Add((tx, tz));
                    }
                }
            }
            N = _tiles.Count * TilePoints;
            _inside = new bool[N];
            foreach (var (zx, zz) in zones)
            {
                Each(zx, zz, (_, p) => _inside[p] = true);
            }
            var points = new List<int>();
            for (int tz = 0; tz < _th; tz++)
            {
                for (int oz = 0; oz < Tile; oz++)
                {
                    for (int tx = 0; tx < _tw; tx++)
                    {
                        int s = _slot[tz * _tw + tx];
                        for (int ox = 0; s >= 0 && ox < Tile; ox++)
                        {
                            int p = s * TilePoints + oz * Tile + ox;
                            if (_inside[p])
                            {
                                points.Add(p);
                            }
                        }
                    }
                }
            }
            Points = [.. points];
        }

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

        /// <summary>The point nearest a world position (halves rounded up, as a disc's middle), or null outside the block's
        /// zones.</summary>
        public int? At(Vector3 v)
        {
            int p = Index(Nearest(v.X) - X0, Nearest(v.Z) - Z0);
            return p >= 0 ? p : null;
        }

        /// <summary>Whether a world position is within margin metres of one of the block's zones (of its square of points).</summary>
        public bool Near(Vector3 v, float margin)
        {
            int zx0 = (int)MathF.Floor((v.X - 32 - margin) / 64) - _zx0, zx1 = (int)MathF.Ceiling((v.X + 32 + margin) / 64) - _zx0;
            int zz0 = (int)MathF.Floor((v.Z - 32 - margin) / 64) - _zz0, zz1 = (int)MathF.Ceiling((v.Z + 32 + margin) / 64) - _zz0;
            for (int tz = Math.Max(0, zz0); tz <= Math.Min(_th - 1, zz1); tz++)
            {
                for (int tx = Math.Max(0, zx0); tx <= Math.Min(_tw - 1, zx1); tx++)
                {
                    int x = X0 + tx * Tile, z = Z0 + tz * Tile;
                    if (_member[tz * _tw + tx] && v.X >= x - margin && v.Z >= z - margin && v.X <= x + Tile + margin && v.Z <= z + Tile + margin)
                    {
                        return true;
                    }
                }
            }
            return false;
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

        /// <summary>The points within r of a disc's middle (as Heightmap.LevelTerrain counts them).</summary>
        public void Disc(Vector3 c, float r, Action<int> f) => Disc(c, r, (p, _) => f(p));

        /// <summary>The points within r of a disc's middle, with their distance from it (metres).</summary>
        public void Disc(Vector3 c, float r, Action<int, float> f)
        {
            int cx = Nearest(c.X) - X0, cz = Nearest(c.Z) - Z0, n = (int)MathF.Ceiling(r);
            for (int z = cz - n; z <= cz + n; z++)
            {
                for (int x = cx - n; x <= cx + n; x++)
                {
                    int p = Index(x, z);
                    if (p < 0)
                    {
                        continue;
                    }
                    float dx = x - cx, dz = z - cz;
                    float d = MathF.Sqrt(dx * dx + dz * dz);
                    if (d <= r)
                    {
                        f(p, d);
                    }
                }
            }
        }

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

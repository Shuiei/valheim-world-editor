namespace TerrainEditor.Editing;

public static partial class Uplift
{
    internal sealed partial class Block
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
    }
}

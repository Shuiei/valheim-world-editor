using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    internal sealed partial class Block
    {
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
    }
}

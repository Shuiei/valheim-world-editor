
namespace TerrainEditor.Desktop;

// Where a world point is on the grid, and the height between grid points.
public static partial class ScriptHost
{
    /// <summary>The grid lookups and the height callbacks of one run.</summary>
    private sealed partial class Callbacks
    {
        /// <summary>The grid point under a world point, or -1 when it is outside the area (or not a point at all).</summary>
        public int Point(float x, float z)
        {
            // Not a point at all (NaN, infinity): would turn into grid column or row 0.
            if (!float.IsFinite(x) || !float.IsFinite(z))
            {
                return -1;
            }
            int gx = (int)MathF.Round(x - _snap.Ox), gz = (int)MathF.Round(z - _snap.Oz);
            return gx < 0 || gz < 0 || gx >= _snap.W || gz >= _snap.H ? -1 : gz * _snap.W + gx;
        }

        /// <summary>Between grid points, the height in between (as the ground is drawn), from the heights <c>a</c>.</summary>
        public float Sample(float[] a, float x, float z)
        {
            float fx = Math.Clamp(x - _snap.Ox, 0, _snap.W - 1), fz = Math.Clamp(z - _snap.Oz, 0, _snap.H - 1);
            int x0 = Math.Min((int)fx, _snap.W - 2), z0 = Math.Min((int)fz, _snap.H - 2);
            float u = fx - x0, v = fz - z0;
            float h00 = a[z0 * _snap.W + x0], h10 = a[z0 * _snap.W + x0 + 1], h01 = a[(z0 + 1) * _snap.W + x0], h11 = a[(z0 + 1) * _snap.W + x0 + 1];
            return (h00 * (1 - u) + h10 * u) * (1 - v) + (h01 * (1 - u) + h11 * u) * v;
        }

        /// <summary>Set: the height of the point under (x, z), unless it is locked or the height is not a number.</summary>
        public void SetHeight(float x, float z, float h)
        {
            int p = Point(x, z);
            if (p >= 0 && !_snap.Locked[p] && float.IsFinite(h))
            {
                lock (_ch)
                {
                    _heights[p] = h;
                    _ch.Heights[p] = h;
                }
            }
        }
    }
}

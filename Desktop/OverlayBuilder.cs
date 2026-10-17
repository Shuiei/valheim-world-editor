using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// Collects one scene's overlay line segments layer by layer, for <see cref="Overlays.Build"/>. Every ring and stripe
/// follows the ground height.
/// </summary>
internal sealed partial class OverlayBuilder
{
    /// <summary>The scene the lines are drawn over.</summary>
    private readonly WorldScene _s;
    /// <summary>The scene's origin in world space, X.</summary>
    private readonly float _ox;
    /// <summary>The scene's origin in world space, Z.</summary>
    private readonly float _oz;
    /// <summary>The line segments collected so far, per layer, as flat x, y, z floats.</summary>
    private readonly Dictionary<Overlays.Layer, List<float>> _lines;

    /// <summary>Starts collecting the lines of a scene.</summary>
    /// <param name="s">The scene to draw over.</param>
    public OverlayBuilder(WorldScene s)
    {
        _s = s;
        _ox = s.OriginX;
        _oz = s.OriginZ;
        _lines = Overlays.All.ToDictionary(l => l, _ => new List<float>());
    }

    /// <summary>The world point on the ground at scene grid point (<paramref name="gx"/>, <paramref name="gz"/>), lifted.</summary>
    /// <remarks>Outside the area: the height at its nearest edge. Rings crossing the edge plunged 1000 m.</remarks>
    /// <param name="gx">The grid point's X.</param>
    /// <param name="gz">The grid point's Z.</param>
    /// <param name="lift">How far above the ground the point sits, in metres.</param>
    /// <returns>The point in view space.</returns>
    private Vector3 OnGround(float gx, float gz, float lift)
    {
        float x = gx - (_s.W - 1) / 2f, z = -(gz - (_s.H - 1) / 2f);
        float cx = Math.Clamp(gx, 0, _s.W - 1) - (_s.W - 1) / 2f, cz = -(Math.Clamp(gz, 0, _s.H - 1) - (_s.H - 1) / 2f);
        return new Vector3(x, Picking.HeightAt(_s, cx, cz) + lift, z);
    }

    /// <summary>Adds a polyline: each consecutive pair of points becomes one segment.</summary>
    /// <param name="l">The layer the segments go to.</param>
    /// <param name="pts">The points, in order.</param>
    internal void Strip(Overlays.Layer l, List<Vector3> pts)
    {
        for (int i = 1; i < pts.Count; i++)
        {
            _lines[l].AddRange([pts[i - 1].X, pts[i - 1].Y, pts[i - 1].Z, pts[i].X, pts[i].Y, pts[i].Z]);
        }
    }

    /// <summary>Adds a ring (or a square) around a world point, following the ground.</summary>
    /// <param name="l">The layer the ring goes to.</param>
    /// <param name="wx">The centre's world X.</param>
    /// <param name="wz">The centre's world Z.</param>
    /// <param name="r">The radius in metres (half the side for a square).</param>
    /// <param name="square">Whether to draw a square instead of a circle.</param>
    private void Ring(Overlays.Layer l, float wx, float wz, float r, bool square = false)
    {
        float gx = wx - _ox, gz = wz - _oz;
        var pts = new List<Vector3>();
        if (square)
        {
            int n = Math.Max(1, (int)MathF.Ceiling(2 * r));
            foreach (var (ax, az, bx, bz) in new[] { (-r, -r, r, -r), (r, -r, r, r), (r, r, -r, r), (-r, r, -r, -r) })
            {
                for (int k = 0; k <= n; k++)
                {
                    pts.Add(OnGround(gx + ax + (bx - ax) * k / n, gz + az + (bz - az) * k / n, 0.4f));
                }
            }
        }
        else
        {
            int n = Math.Max(24, (int)MathF.Ceiling(2 * MathF.PI * r));
            for (int k = 0; k <= n; k++)
            {
                float a = k / (float)n * MathF.Tau;
                pts.Add(OnGround(gx + MathF.Cos(a) * r, gz + MathF.Sin(a) * r, 0.4f));
            }
        }
        Strip(l, pts);
    }

    /// <summary>The collected lines, per layer, as flat x, y, z floats.</summary>
    /// <returns>Each layer's segments.</returns>
    public Dictionary<Overlays.Layer, float[]> Finish() => _lines.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
}

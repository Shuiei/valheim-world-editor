using System.Numerics;

namespace Tunnelheim.WorldEditor;

/// <summary>The rock's density between its samples, and the surface's normal from it (RockGrid.Normal.cs).</summary>
public sealed partial class RockGrid
{
    /// <summary>
    /// The density at any point (VoxelWorld.SampleDensity): the eight samples around it on the half-metre grid, blended
    /// by where the point lies between them.
    /// </summary>
    /// <param name="p">The point, in world metres.</param>
    public float SampleDensity(Vector3 p)
    {
        float gx = p.X / Step, gy = p.Y / Step, gz = p.Z / Step;
        int x = RockMath.Floor(gx), y = RockMath.Floor(gy), z = RockMath.Floor(gz);
        float fx = gx - x, fy = gy - y, fz = gz - z;
        float Lerp2(int dy, int dz) => RockMath.Lerp(DensityAt(x, y + dy, z + dz), DensityAt(x + 1, y + dy, z + dz), fx);
        float low = RockMath.Lerp(Lerp2(0, 0), Lerp2(1, 0), fy), high = RockMath.Lerp(Lerp2(0, 1), Lerp2(1, 1), fy);
        return RockMath.Lerp(low, high, fz);
    }

    /// <summary>
    /// The rock surface's normal at a point (VoxelWorld.SurfaceNormal), pointing into the air: the density's slope a
    /// metre either way along each axis, reversed and made unit length. The fallback when the rock is flat there, or
    /// when the normal faces away from it.
    /// </summary>
    /// <param name="p">The point, in world metres.</param>
    /// <param name="fallback">The normal to use otherwise (unit length).</param>
    public Vector3 SurfaceNormal(Vector3 p, Vector3 fallback)
    {
        var g = new Vector3(
            SampleDensity(p + Vector3.UnitX) - SampleDensity(p - Vector3.UnitX),
            SampleDensity(p + Vector3.UnitY) - SampleDensity(p - Vector3.UnitY),
            SampleDensity(p + Vector3.UnitZ) - SampleDensity(p - Vector3.UnitZ));
        if (RockMath.LengthSquared(g) < 1e-6f)
        {
            return fallback;
        }
        var n = -RockMath.Normalize(g);
        return RockMath.Dot(n, fallback) < 0 ? fallback : n;
    }
}

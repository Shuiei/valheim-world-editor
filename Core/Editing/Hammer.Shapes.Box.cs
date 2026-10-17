using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    /// <summary>A ray against a turned box (slab test); null when it misses or starts inside.</summary>
    private static (float, Vector3)? RayBox(Vector3 o, Vector3 d, Box b)
    {
        var inv = Quaternion.Inverse(b.Q);
        var lo = Vector3.Transform(o - b.C, inv);
        var ld = Vector3.Transform(d, inv);
        float tmin = float.MinValue, tmax = float.MaxValue;
        Vector3 n = Vector3.Zero;
        for (int k = 0; k < 3; k++)
        {
            float oo = k == 0 ? lo.X : k == 1 ? lo.Y : lo.Z, dd = k == 0 ? ld.X : k == 1 ? ld.Y : ld.Z, h = k == 0 ? b.H.X : k == 1 ? b.H.Y : b.H.Z;
            if (MathF.Abs(dd) < 1e-9f)
            {
                if (oo < -h || oo > h)
                {
                    return null;
                }
                continue;
            }
            float t1 = (-h - oo) / dd, t2 = (h - oo) / dd;
            float sign = -1;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
                sign = 1;
            }
            if (t1 > tmin)
            {
                tmin = t1;
                n = k == 0 ? new Vector3(sign, 0, 0) : k == 1 ? new Vector3(0, sign, 0) : new Vector3(0, 0, sign);
            }
            tmax = MathF.Min(tmax, t2);
            if (tmin > tmax)
            {
                return null;
            }
        }
        return tmin < 0 ? null : (tmin, Vector3.Transform(n, b.Q));
    }
}

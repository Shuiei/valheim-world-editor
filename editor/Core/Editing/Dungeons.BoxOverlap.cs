using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>Separating axis test of two turned boxes (centre, half size, rotation); touching faces do not count.</summary>
    public static bool BoxesOverlap((Vector3 C, Vector3 H, Quaternion R) a, (Vector3 C, Vector3 H, Quaternion R) b)
    {
        Vector3[] ax = [Vector3.Transform(Vector3.UnitX, a.R), Vector3.Transform(Vector3.UnitY, a.R), Vector3.Transform(Vector3.UnitZ, a.R)];
        Vector3[] bx = [Vector3.Transform(Vector3.UnitX, b.R), Vector3.Transform(Vector3.UnitY, b.R), Vector3.Transform(Vector3.UnitZ, b.R)];
        Vector3 d = b.C - a.C;
        float[] ha = [a.H.X, a.H.Y, a.H.Z], hb = [b.H.X, b.H.Y, b.H.Z];
        bool Separated(Vector3 axis)
        {
            if (axis.LengthSquared() < 1e-8f)
            {
                return false;
            }
            float ra = 0, rb = 0;
            for (int i = 0; i < 3; i++)
            {
                ra += ha[i] * MathF.Abs(Vector3.Dot(ax[i], axis));
                rb += hb[i] * MathF.Abs(Vector3.Dot(bx[i], axis));
            }
            // Touching faces do not overlap (rooms that join share a wall plane).
            return MathF.Abs(Vector3.Dot(d, axis)) >= ra + rb - 1e-3f * axis.Length();
        }
        for (int i = 0; i < 3; i++)
        {
            if (Separated(ax[i]) || Separated(bx[i]))
            {
                return false;
            }
        }
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (Separated(Vector3.Cross(ax[i], bx[j])))
                {
                    return false;
                }
            }
        }
        return true;
    }
}

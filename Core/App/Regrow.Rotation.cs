using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.App;

/// <summary>
/// The rotation of a placed vegetation object: the game's ground-tilt alignment and Unity's LookRotation.
/// </summary>
public static partial class Regrow
{
    /// <summary>Unity's Quaternion.LookRotation(forward, up).</summary>
    private static NQuaternion LookRotation(NVector3 forward, NVector3 up)
    {
        var z = NVector3.Normalize(forward);
        var x = NVector3.Normalize(NVector3.Cross(up, z));
        var y = NVector3.Cross(z, x);
        // Columns x, y, z of the rotation matrix.
        float m00 = x.X, m01 = y.X, m02 = z.X, m10 = x.Y, m11 = y.Y, m12 = z.Y, m20 = x.Z, m21 = y.Z, m22 = z.Z;
        float trace = m00 + m11 + m22;
        if (trace > 0f)
        {
            float s = MathF.Sqrt(trace + 1f) * 2f;
            return new NQuaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
        }
        if (m00 > m11 && m00 > m22)
        {
            float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
            return new NQuaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
        }
        if (m11 > m22)
        {
            float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
            return new NQuaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
        }
        float s2 = MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
        return new NQuaternion((m02 + m20) / s2, (m12 + m21) / s2, 0.25f * s2, (m10 - m01) / s2);
    }

    /// <summary>
    /// The rotation a placed object gets: turned to the ground's slope (LookRotation of the normal) when the rule's
    /// chance for that comes up, else the random tilt and turn of the draw.
    /// </summary>
    private static NVector3 SpotRotation(Rule veg, NVector3 normal, SpotDraw draw)
    {
        if (veg.chanceToUseGroundTilt > 0f && Rnd.Value <= veg.chanceToUseGroundTilt)
        {
            // Quaternion.LookRotation(Cross(normal, Euler(0, y, 0) * forward), normal).
            float a = draw.Ry * MathF.PI / 180f;
            NVector3 fwd = new(MathF.Sin(a), 0f, MathF.Cos(a));
            return BlueprintFormats.ToEuler(LookRotation(NVector3.Cross(normal, fwd), normal));
        }
        return new NVector3(draw.Rx, draw.Ry, draw.Rz);
    }
}

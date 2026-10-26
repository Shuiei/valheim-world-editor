using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Gizmo's picking: which handle a mouse ray grabs, and where along an axis or around the ring it points.
/// </summary>
public static partial class Gizmo
{
    /// <summary>
    /// The handle a ray from the mouse grabs (the arrows a little thicker than drawn, the nearest along the ray first;
    /// the ring only when no arrow is hit), or null.
    /// </summary>
    /// <param name="o">Where the ray starts, in view space.</param>
    /// <param name="d">Its direction, of length 1.</param>
    /// <param name="centre">The gizmo's middle.</param>
    /// <param name="scale">The gizmo's scale (as drawn).</param>
    public static Handle? Hit(Vector3 o, Vector3 d, Vector3 centre, float scale)
    {
        Handle? best = null;
        float bestT = float.MaxValue;
        foreach (var h in new[] { Handle.X, Handle.Y, Handle.Z })
        {
            var a = Axis(h);
            var (t, s, dist) = Closest(o, d, centre, a);
            if (s >= ShaftFrom * scale && s <= HeadTo * scale && dist <= 0.09f * scale && t > 0 && t < bestT)
            {
                best = h;
                bestT = t;
            }
        }
        if (best != null)
        {
            return best;
        }
        return RingHit(o, d, centre, scale) ? Handle.Ring : null;
    }

    /// <summary>
    /// Whether the ray meets the flat plane through the middle ahead of it, near the ring's radius.
    /// </summary>
    /// <param name="o">Where the ray starts, in view space.</param>
    /// <param name="d">Its direction, of length 1.</param>
    /// <param name="centre">The gizmo's middle.</param>
    /// <param name="scale">The gizmo's scale (as drawn).</param>
    internal static bool RingHit(Vector3 o, Vector3 d, Vector3 centre, float scale)
    {
        if (MathF.Abs(d.Y) <= 1e-4f)
        {
            return false;
        }
        float t = (centre.Y - o.Y) / d.Y;
        if (t <= 0)
        {
            return false;
        }
        var p = o + d * t;
        float r = MathF.Sqrt((p.X - centre.X) * (p.X - centre.X) + (p.Z - centre.Z) * (p.Z - centre.Z));
        return MathF.Abs(r - RingRadius * scale) <= 0.11f * scale;
    }

    /// <summary>
    /// The closest points of the ray (o + d t) and the axis line (c + a s): t, s and the distance between them. d and a
    /// are of length 1; parallel, t is 0.
    /// </summary>
    public static (float T, float S, float Distance) Closest(Vector3 o, Vector3 d, Vector3 c, Vector3 a)
    {
        var w = o - c;
        float b = Vector3.Dot(a, d), den = 1 - b * b;
        if (den < 1e-6f)
        {
            return (0, Vector3.Dot(w, a), Vector3.Cross(w, a).Length());
        }
        float dw = Vector3.Dot(d, w), aw = Vector3.Dot(a, w);
        float t = (b * aw - dw) / den, s = (aw - b * dw) / den;
        return (t, s, Vector3.Distance(o + d * t, c + a * s));
    }

    /// <summary>
    /// How far along the axis (through c) the mouse ray passes closest, or null looking straight down it.
    /// </summary>
    public static float? Along(Vector3 o, Vector3 d, Vector3 c, Vector3 a)
    {
        float b = Vector3.Dot(a, d);
        return 1 - b * b < 1e-4f ? null : Closest(o, d, c, a).S;
    }

    /// <summary>
    /// The heading (Unity yaw, degrees: clockwise from north seen from above, -180..180) from the middle to where the
    /// ray meets the flat plane through it, or null when it does not (looking level, or away from it).
    /// </summary>
    public static float? Heading(Vector3 o, Vector3 d, Vector3 centre)
    {
        if (MathF.Abs(d.Y) < 1e-4f)
        {
            return null;
        }
        float t = (centre.Y - o.Y) / d.Y;
        if (t <= 0)
        {
            return null;
        }
        var p = o + d * t;
        return MathF.Atan2(p.X - centre.X, -(p.Z - centre.Z)) * 180 / MathF.PI;
    }
}

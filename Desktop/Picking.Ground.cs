using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>The ground hit of the picking: where a ray meets the area's ground.</summary>
public static partial class Picking
{
    /// <summary>
    /// Where the ray meets the ground (distance), stepping half a metre then narrowing down, or null. Only the area's
    /// ground: outside it, where HeightAt is very low, the ray never hits (it hit 1000 m down past the edge, a point
    /// the tools then took).
    /// </summary>
    /// <param name="s">The scene whose ground to test.</param>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's unit direction.</param>
    /// <param name="maxDist">How far along the ray to look, in metres.</param>
    public static float? HitGround(WorldScene s, Vector3 o, Vector3 d, float maxDist = 4000)
    {
        // The height map is crossed wherever it is; where a mod's extension cuts the ground away, the crossing is
        // no hit (the ray goes on, into the hole), and the next crossing counts.
        float Below(float t)
        {
            var p = o + d * t;
            return Inside(s, p.X, p.Z) ? p.Y - HeightAt(s, p.X, p.Z) : float.PositiveInfinity;
        }
        float prev = 0;
        float above = Below(0);
        for (float t = 0.5f; t <= maxDist; t += 0.5f)
        {
            float b = Below(t);
            if (b <= 0 && above > 0)
            {
                float lo = prev, hi = t;
                for (int i = 0; i < 20; i++)
                {
                    float mid = (lo + hi) / 2;
                    if (Below(mid) > 0)
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid;
                    }
                }
                float hit = (lo + hi) / 2;
                var at = o + d * hit;
                if (!CutAt(s, at.X, at.Z))
                {
                    return hit;
                }
            }
            prev = t;
            above = b;
        }
        return null;
    }
}

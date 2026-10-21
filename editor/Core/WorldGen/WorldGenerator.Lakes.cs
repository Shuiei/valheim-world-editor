
namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // ---- Lakes: every 128 m where the continents are under 0.05, nearby ones merged.
    /// <summary>
    /// Every 128 m (the rules' interval) over the world's radius in world metres where the continents are under 0.05
    /// (the rules' lake depth), within 800 m of each other merged. The scan's points are world metres (Expand World
    /// Size leaves the loop alone and stretches the point only for the continents' height).
    /// </summary>
    private List<Vector2> FindLakes()
    {
        var low = new List<Vector2>();
        for (float z = -_v.Radius; z <= _v.Radius; z = (float)((double)z + _v.LakeStep))
        {
            for (float x = -_v.Radius; x <= _v.Radius; x = (float)((double)x + _v.LakeStep))
            {
                if (!(GenMath.Length(x, z) > _v.Radius) && Continent(Stretched(x), Stretched(z)) < _v.LakeDepth)
                {
                    low.Add(new Vector2(x, z));
                }
            }
        }
        return Merge(low, _v.LakeMerge);
    }

    /// <summary>
    /// The nearest other point within range (the first of equals), or -1.
    /// </summary>
    internal static int Nearest(List<Vector2> points, Vector2 p, float range)
    {
        int best = -1;
        float bestDistance = 99999f;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == p)
            {
                continue;
            }
            float d = Vector2.Distance(p, points[i]);
            if (d < range && d < bestDistance)
            {
                best = i;
                bestDistance = d;
            }
        }
        return best;
    }
}

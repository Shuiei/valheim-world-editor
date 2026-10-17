
namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // ---- Lakes: every 128 m where the continents are under 0.05, nearby ones merged.
    /// <summary>
    /// Every 128 m where the continents are under 0.05, within 800 m of each other merged.
    /// </summary>
    private List<Vector2> FindLakes()
    {
        var low = new List<Vector2>();
        for (float z = -WorldSize; z <= WorldSize; z = (float)((double)z + 128.0))
        {
            for (float x = -WorldSize; x <= WorldSize; x = (float)((double)x + 128.0))
            {
                if (!(GenMath.Length(x, z) > WorldSize) && Continent(x, z) < 0.05f)
                {
                    low.Add(new Vector2(x, z));
                }
            }
        }
        return Merge(low, 800f);
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

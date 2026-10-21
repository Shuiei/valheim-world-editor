using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public sealed partial class WorldGenerator
{
    /// <summary>
    /// A random lake (index) within range of p that no river joins to p yet and whose way a river can run, or -1.
    /// </summary>
    private int RandomRiverEnd(List<River> rivers, Vector2 p, float range)
    {
        var ends = new List<int>();
        for (int i = 0; i < _lakes.Count; i++)
        {
            var q = _lakes[i];
            if (q != p && Vector2.Distance(p, q) < range && !rivers.Any(r => (r.P0 == p && r.P1 == q) || (r.P0 == q && r.P1 == p)) && RiverCanRun(p, q))
            {
                ends.Add(i);
            }
        }
        return ends.Count == 0 ? -1 : ends[Rnd.Range(0, ends.Count)];
    }

    /// <summary>
    /// Along the way (every 128 m, the rules' interval, ends left out): never over 0.4 (the rules' river limit),
    /// and out of the water (over the rules' lake depth) at least once. The lakes are world metres; the continents'
    /// height is read at the stretched point.
    /// </summary>
    private bool RiverCanRun(Vector2 a, Vector2 b)
    {
        float step = _v.RiverStep;
        float length = Vector2.Distance(a, b);
        var dir = (b - a).Normalized;
        bool allWater = true;
        for (float s = step; s <= (float)((double)length - step); s = (float)((double)s + step))
        {
            var q = a + dir * s;
            float land = Continent(Stretched(q.X), Stretched(q.Y));
            if (land > _v.RiverLimit)
            {
                return false;
            }
            allWater &= !(land > _v.LakeDepth);
        }
        return !allWater;
    }
}

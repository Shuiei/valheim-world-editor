using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // ---- Rivers: from each lake to a random lake within 2 km (5 km for one with no river yet) whose
    // way stays low and leaves the water somewhere.
    /// <summary>
    /// Lays out the rivers from the rivers' seed and draws them, leaving this thread's Random state as it was.
    /// </summary>
    private List<River> PlaceRivers()
    {
        var saved = Rnd.State;
        try
        {
            Rnd.InitState(_riverSeed);
            var rivers = new List<River>();
            var from = new List<Vector2>(_lakes);
            while (from.Count > 1)
            {
                var p = from[0];
                int to = RandomRiverEnd(rivers, p, 2000f);
                if (to == -1 && !rivers.Any(r => r.P0 == p || r.P1 == p))
                {
                    to = RandomRiverEnd(rivers, p, 5000f);
                }
                if (to == -1)
                {
                    from.RemoveAt(0);
                    continue;
                }
                var river = new River { P0 = p, P1 = _lakes[to] };
                river.Center = (river.P0 + river.P1) * 0.5f;
                river.WidthMax = Rnd.Range(60f, 100f);
                river.WidthMin = Rnd.Range(60f, river.WidthMax);
                Curves(river);
                rivers.Add(river);
            }
            Draw(rivers, Streams.All);
            return rivers;
        }
        finally
        {
            Rnd.State = saved;
        }
    }

    /// <summary>
    /// Points within range of each other merged into one (halfway each time), in the game's order.
    /// Empties <paramref name="points"/>.
    /// </summary>
    internal static List<Vector2> Merge(List<Vector2> points, float range)
    {
        var merged = new List<Vector2>();
        while (points.Count > 0)
        {
            var p = points[0];
            points.RemoveAt(0);
            for (int near; points.Count > 0 && (near = Nearest(points, p, range)) != -1;)
            {
                p = (p + points[near]) * 0.5f;
                points[near] = points[^1];
                points.RemoveAt(points.Count - 1);
            }
            merged.Add(p);
        }
        return merged;
    }

    /// <summary>
    /// A river's waves: a fifteenth of its length wide, a twentieth long.
    /// </summary>
    private static void Curves(River river)
    {
        float length = Vector2.Distance(river.P0, river.P1);
        river.CurveWidth = (float)((double)length / 15.0);
        river.CurveWavelength = (float)((double)length / 20.0);
    }
}

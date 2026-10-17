using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // ---- Streams: 3000 tries for a start on low slopes (26 to 31 m) and an end higher up (36 to 44 m,
    // 80 to 200 m away), their middle between those heights.
    /// <summary>
    /// Lays out the streams from the streams' seed and draws those in (or outside) the Deep North, leaving this
    /// thread's Random state as it was.
    /// </summary>
    /// <param name="deepNorth">Lay out with the Deep North raised and draw only the streams starting in it, instead
    /// of the others.</param>
    /// <returns>Every stream laid out, also those not drawn.</returns>
    private List<River> PlaceStreams(bool deepNorth)
    {
        var saved = Rnd.state;
        try
        {
            Rnd.InitState(_streamSeed);
            var streams = new List<River>();
            for (int i = 0; i < 3000; i++)
            {
                if (!StreamStart(!deepNorth, out var start) || !StreamEnd(start, !deepNorth, out var end))
                {
                    continue;
                }
                var middle = (start + end) * 0.5f;
                float h = GetPregenerationHeight(middle.x, middle.y, !deepNorth);
                if (h is < 26f or > 44f)
                {
                    continue;
                }
                var stream = new River { P0 = start, P1 = end, Center = middle, WidthMax = 20f, WidthMin = 20f };
                Curves(stream);
                streams.Add(stream);
            }
            Draw(streams, deepNorth ? Streams.DeepNorthOnly : Streams.NotDeepNorth);
            return streams;
        }
        finally
        {
            Rnd.state = saved;
        }
    }

    /// <summary>
    /// Up to 100 random points for one on low slopes (26 to 31 m); false when none was.
    /// </summary>
    private bool StreamStart(bool forRivers, out Vector2 start)
    {
        for (int i = 0; i < 100; i++)
        {
            float x = Rnd.Range(-10000f, 10000f), z = Rnd.Range(-10000f, 10000f);
            float h = GetPregenerationHeight(x, z, forRivers);
            if (h is > 26f and < 31f)
            {
                start = new Vector2(x, z);
                return true;
            }
        }
        start = Vector2.zero;
        return false;
    }

    /// <summary>
    /// An end higher up (36 to 44 m), tried at random directions, from 200 m down towards 80 m; false when none was.
    /// </summary>
    private bool StreamEnd(Vector2 start, bool forRivers, out Vector2 end)
    {
        const float Longest = 200f, Shortest = 80f;
        const int Tries = 100;
        float step = (float)(((double)Longest - Shortest) / Tries);
        float length = Longest;
        for (int i = 0; i < Tries; i++)
        {
            length = (float)((double)length - step);
            float angle = Rnd.Range(0f, MathF.PI * 2f);
            var q = start + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * length;
            float h = GetPregenerationHeight(q.x, q.y, forRivers);
            if (h is > 36f and < 44f)
            {
                end = q;
                return true;
            }
        }
        end = Vector2.zero;
        return false;
    }
}

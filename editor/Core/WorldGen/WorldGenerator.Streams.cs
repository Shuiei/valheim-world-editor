using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // ---- Streams: 3000 tries for a start on low slopes (26 to 31 m) and an end higher up (36 to 44 m,
    // 80 to 200 m away), their middle between those heights. The counts and heights are the rules'.
    /// <summary>
    /// Lays out the streams from the streams' seed and draws those in (or outside) the Deep North, leaving this
    /// thread's Random state as it was. None when the rules turn streams off.
    /// </summary>
    /// <param name="deepNorth">Lay out with the Deep North raised and draw only the streams starting in it, instead
    /// of the others.</param>
    /// <returns>Every stream laid out, also those not drawn.</returns>
    private List<River> PlaceStreams(bool deepNorth)
    {
        if (!_v.StreamsOn)
        {
            return [];
        }
        var saved = Rnd.State;
        try
        {
            Rnd.InitState(_streamSeed);
            var streams = new List<River>();
            for (int i = 0; i < _v.StreamTries; i++)
            {
                if (!StreamStart(!deepNorth, out var start) || !StreamEnd(start, !deepNorth, out var end))
                {
                    continue;
                }
                var middle = (start + end) * 0.5f;
                float h = GetPregenerationHeight(middle.X, middle.Y, !deepNorth);
                if (h < _v.StreamStartMin || h > _v.StreamEndMax)
                {
                    continue;
                }
                // The game sets both widths to 20; Expand World Rivers' stream width replaces the first only.
                var stream = new River { P0 = start, P1 = end, Center = middle, WidthMax = _v.StreamWidthMax, WidthMin = 20f };
                Curves(stream, _v.StreamCurveWidth, _v.StreamCurveWaveLength);
                streams.Add(stream);
            }
            Draw(streams, deepNorth ? Streams.DeepNorthOnly : Streams.NotDeepNorth);
            return streams;
        }
        finally
        {
            Rnd.State = saved;
        }
    }

    /// <summary>
    /// Up to 100 random points (the rules' count) over the world's radius for one on low slopes (26 to 31 m, the
    /// rules' window); false when none was.
    /// </summary>
    private bool StreamStart(bool forRivers, out Vector2 start)
    {
        for (int i = 0; i < _v.StreamIterations; i++)
        {
            float x = Rnd.Range(-_v.Radius, _v.Radius), z = Rnd.Range(-_v.Radius, _v.Radius);
            float h = GetPregenerationHeight(x, z, forRivers);
            if (h > _v.StreamStartMin && h < _v.StreamStartMax)
            {
                start = new Vector2(x, z);
                return true;
            }
        }
        start = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// An end higher up (36 to 44 m, the rules' window), tried at random directions, from 200 m down towards 80 m
    /// (the rules' lengths); false when none was.
    /// </summary>
    private bool StreamEnd(Vector2 start, bool forRivers, out Vector2 end)
    {
        float longest = _v.StreamMaxLength, shortest = _v.StreamMinLength;
        int tries = _v.StreamIterations;
        float step = (float)(((double)longest - shortest) / tries);
        float length = longest;
        for (int i = 0; i < tries; i++)
        {
            length = (float)((double)length - step);
            float angle = Rnd.Range(0f, MathF.PI * 2f);
            var q = start + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * length;
            float h = GetPregenerationHeight(q.X, q.Y, forRivers);
            if (h > _v.StreamEndMin && h < _v.StreamEndMax)
            {
                end = q;
                return true;
            }
        }
        end = Vector2.Zero;
        return false;
    }
}

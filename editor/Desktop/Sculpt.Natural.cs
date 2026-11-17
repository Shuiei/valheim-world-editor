namespace TerrainEditor.Desktop;

// Naturalize: the height each point is pulled towards, worked out once per stroke.
public static partial class Sculpt
{
    /// <summary>
    /// Naturalize's aim: the ground's broad shape (its average round the point, from the start of the stroke) plus the
    /// brush's bumps, as the Area and Path tools aim (<see cref="GroundOps.NaturalTarget"/>). Kept for the stroke, so
    /// going over a point again pulls it to the same height.
    /// </summary>
    private static float NaturalTarget(Ground g, Brush b, Stroke s, int p)
    {
        if (s.Natural.TryGetValue(p, out float t))
        {
            return t;
        }
        t = GroundOps.NaturalTarget(g, b, p, GroundOps.Average(g.W, g.H, p, GroundOps.NaturalReach(b), i => s.StartHeight(g, i)));
        s.Natural[p] = t;
        return t;
    }
}

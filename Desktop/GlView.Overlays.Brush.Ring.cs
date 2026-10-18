namespace TerrainEditor.Desktop;

// The brush's ring as line pairs on the ground, lifted a little so it shows over the ground.
public sealed partial class GlView
{
    /// <summary>
    /// The brush's ring as line pairs on the ground: the outline of the brush (or the ring of the Shape tool), and its
    /// inner edge when the brush has one. Each segment is laid where the ground is, none where there is no ground.
    /// </summary>
    /// <param name="s">The area.</param>
    /// <param name="h">The grid point under the pointer.</param>
    /// <param name="session">The edit session, whose brush gives the outline.</param>
    /// <param name="n">How many points the outline has.</param>
    /// <returns>The segments, six floats each.</returns>
    private float[] BrushRing(WorldScene s, (float X, float Z) h, EditSession session, int n)
    {
        var data = new List<float>();
        void Loop(List<(float X, float Z)> pts)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                var (ax, az) = At(pts[i]);
                var (bx, bz) = At(pts[(i + 1) % pts.Count]);
                float ya = Picking.HeightAt(s, ax, az), yb = Picking.HeightAt(s, bx, bz);
                if (ya <= -1000 || yb <= -1000)
                {
                    continue;
                }
                data.AddRange([ax, ya + 0.3f, az, bx, yb + 0.3f, bz]);
            }
        }
        (float, float) At((float X, float Z) o) => GridToView(s, h.X + o.X, h.Z + o.Z);
        Loop(Tool == null ? [.. Enumerable.Range(0, n).Select(i => (MathF.Cos(i * MathF.Tau / n) * ShapeRadius, MathF.Sin(i * MathF.Tau / n) * ShapeRadius))] : session.Brush.Outline(n));
        if (Tool != null && session.Brush.InnerOutline(n) is { } inner)
        {
            Loop(inner);
        }
        return [.. data];
    }
}

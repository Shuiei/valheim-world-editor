
namespace TerrainEditor.Desktop;

// The brush's outline on the ground: the full shape, and the Ring shape's inner edge.
public sealed partial class Brush
{
    /// <summary>The outline drawn on the ground: n points around the brush, as offsets from its middle.</summary>
    public List<(float X, float Z)> Outline(int n)
    {
        var pts = new List<(float, float)>();
        float a = Turn * MathF.PI / 180;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n * MathF.Tau, x = MathF.Cos(t), z = MathF.Sin(t);
            if (Turnable)
            {
                float m = MathF.Max(MathF.Abs(x), MathF.Abs(z));
                x /= m;
                z /= m;
                (x, z) = (x * MathF.Cos(a) - z * MathF.Sin(a), x * MathF.Sin(a) + z * MathF.Cos(a));
            }
            pts.Add((x * Radius, z * Radius));
        }
        return pts;
    }

    /// <summary>The Ring shape works on a band from 40% of the radius to the edge: its inner edge, or null.</summary>
    public List<(float X, float Z)>? InnerOutline(int n) => Shape != BrushShape.Ring ? null
        : Enumerable.Range(0, n).Select(i => (MathF.Cos(i / (float)n * MathF.Tau) * Radius * 0.4f, MathF.Sin(i / (float)n * MathF.Tau) * Radius * 0.4f)).ToList();
}

using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>
    /// The dense line (about 1 m apart): a Catmull-Rom curve through the points, or straight stretches; Seg is the
    /// index of the point each stretch starts from.
    /// </summary>
    public List<(Vector2 P, int Seg)> Curve()
    {
        var p = Points;
        var outp = new List<(Vector2, int)>();
        if (p.Count < 2)
        {
            outp.AddRange(p.Select(q => (q, 0)));
            return outp;
        }
        if (Curved && p.Count >= 3)
        {
            return Lines.CatmullRom(p);
        }
        for (int i = 0; i < p.Count - 1; i++)
        {
            Vector2 p1 = p[i], p2 = p[i + 1];
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(p1, p2)));
            for (int k = 0; k < steps; k++)
            {
                outp.Add((p1 + (p2 - p1) * (k / (float)steps), i));
            }
        }
        outp.Add((p[^1], p.Count - 2));
        return outp;
    }

    /// <summary>The length of a dense line, in metres.</summary>
    public static float Length(List<(Vector2 P, int Seg)> curve)
    {
        float l = 0;
        for (int i = 1; i < curve.Count; i++)
        {
            l += Vector2.Distance(curve[i].P, curve[i - 1].P);
        }
        return l;
    }
}

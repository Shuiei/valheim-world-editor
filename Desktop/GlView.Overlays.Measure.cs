using System.Numerics;

namespace TerrainEditor.Desktop;

// The Measure tool's tape.
public sealed partial class GlView
{
    /// <summary>The tape's lines on the graphics card.</summary>
    private uint _measureVao, _measureVbo;
    /// <summary>
    /// The tape: following the ground from A to B (its profile), and a cross at each end. Shown in the
    /// Measure tool, and kept once fixed.
    /// </summary>
    private void DrawMeasure(WorldScene s, Matrix4x4 vp)
    {
        var (a, b) = (Tape.A, Tape.B);
        if (a == null || Mode != ToolMode.Measure && !Tape.Fixed)
        {
            return;
        }
        var data = new List<float>();
        Vector3 V(float wx, float wz) => new(wx - s.Cx, GroundHeight(wx, wz) + 0.2f, -(wz - s.Cz));
        void Seg(Vector3 p, Vector3 q) => data.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
        foreach (var e in new[] { a, b })
        {
            if (e is { } p)
            {
                var c = V(p.X, p.Z);
                Seg(c - new Vector3(0.6f, 0, 0), c + new Vector3(0.6f, 0, 0));
                Seg(c - new Vector3(0, 0, 0.6f), c + new Vector3(0, 0, 0.6f));
                Seg(c, c + new Vector3(0, 1.5f, 0));
            }
        }
        if (a is { } pa && b is { } pb)
        {
            int n = Math.Max(2, (int)MathF.Ceiling(Vector2.Distance(new Vector2(pa.X, pa.Z), new Vector2(pb.X, pb.Z))));
            for (int i = 0; i < n; i++)
            {
                float t0 = i / (float)n, t1 = (i + 1) / (float)n;
                Seg(V(pa.X + (pb.X - pa.X) * t0, pa.Z + (pb.Z - pa.Z) * t0), V(pa.X + (pb.X - pa.X) * t1, pa.Z + (pb.Z - pa.Z) * t1));
            }
        }
        DrawLines(ref _measureVao, ref _measureVbo, [.. data], vp, new Vector4(1, 0.88f, 0.54f, 1));
    }
}

using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The Path tool's marks: the ground it covers, its edges, its centre line and points, the next point's preview.
public sealed partial class GlView
{
    /// <summary>The Path tool's lines on the graphics card: centre line, edges, points.</summary>
    private uint _pathVao, _pathVbo, _pathEdgeVao, _pathEdgeVbo, _pathDotVao, _pathDotVbo;
    /// <summary>
    /// The Path tool's bands on the graphics card: the ground it covers, the soft edge, and the next point's preview.
    /// </summary>
    private uint _pathBandVao, _pathBandVbo, _pathSoftVao, _pathSoftVbo, _pathPreviewVao, _pathPreviewVbo, _pathPreviewSoftVao, _pathPreviewSoftVbo;

    /// <summary>
    /// The Path tool: the ground it covers (a light band), its width's edges and the soft edge's outer
    /// line, the centre line (red) and its points; under the cursor, the width (and the soft edge) the
    /// next point brings, with the stretch from the last point to it. Lines are flat ribbons on the
    /// ground a few pixels wide, so they stay whole at any resolution and angle (1-pixel lines broke
    /// into dashes once the view was scaled up).
    /// </summary>
    private void DrawPath(WorldScene s, Matrix4x4 vp)
    {
        if (Mode != ToolMode.Path)
        {
            return;
        }
        List<(Vector2 P, int Seg)> curve;
        List<Vector2> pts;
        lock (Path.Points)
        {
            curve = Path.Curve();
            pts = [.. Path.Points];
        }
        Vector3 V(Vector2 g, float lift) => OnGround(s, g.X, g.Y, lift);
        // Grid points every metre or so along a line (the ground's own spacing), so a ribbon follows it.
        static List<Vector2> Dense(IReadOnlyList<Vector2> line)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < line.Count; i++)
            {
                if (i > 0)
                {
                    int n = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(line[i - 1], line[i])));
                    for (int k = 1; k < n; k++)
                    {
                        o.Add(Vector2.Lerp(line[i - 1], line[i], k / (float)n));
                    }
                }
                o.Add(line[i]);
            }
            return o;
        }
        // A line as a ribbon of triangles, its width a fixed share of the distance to the eye.
        void Ribbon(List<float> to, IReadOnlyList<Vector2> line, float px, float lift)
        {
            var d = Dense(line);
            for (int i = 1; i < d.Count; i++)
            {
                Vector2 a = d[i - 1], b = d[i];
                float len = Vector2.Distance(a, b);
                if (len < 1e-5f)
                {
                    continue;
                }
                Vector3 va = V(a, lift), vb = V(b, lift);
                Vector2 n = new Vector2(-(b.Y - a.Y), b.X - a.X) / len;
                float wa = Vector3.Distance(_lastEye, va) * px, wb = Vector3.Distance(_lastEye, vb) * px;
                Vector3 Off(Vector2 g, float w, Vector3 at) { var q = V(g + n * w, lift); return new Vector3(q.X, at.Y, q.Z); }
                Vector3 a0 = Off(a, -wa, va), a1 = Off(a, wa, va), b0 = Off(b, -wb, vb), b1 = Off(b, wb, vb);
                foreach (var v in new[] { a0, a1, b1, a0, b1, b0 })
                {
                    to.AddRange([v.X, v.Y, v.Z]);
                }
            }
        }
        // The line offset to one side (+1 left, -1 right) by a distance, point by point.
        static List<Vector2> Offset(List<Vector2> line, float by)
        {
            var o = new List<Vector2>(line.Count);
            for (int i = 0; i < line.Count; i++)
            {
                Vector2 dir = line[Math.Min(i + 1, line.Count - 1)] - line[Math.Max(i - 1, 0)];
                float len = dir.Length();
                o.Add(len < 1e-6f ? line[i] : line[i] + new Vector2(-dir.Y, dir.X) / len * by);
            }
            return o;
        }
        static List<Vector2> Circle(Vector2 c, float r)
        {
            int n = Math.Clamp((int)(r * 6), 32, 160);
            return [.. Enumerable.Range(0, n + 1).Select(i => c + new Vector2(MathF.Cos(i * MathF.Tau / n), MathF.Sin(i * MathF.Tau / n)) * r)];
        }
        float half = Path.Width / 2, outer = half + MathF.Max(0, Path.Soft);
        float thick = _linePx * 1.6f, thin = _linePx * 0.9f;
        var band = new List<float>();
        var edges = new List<float>();
        var soft = new List<float>();
        var line = new List<float>();
        void Stretch(List<Vector2> c)
        {
            if (c.Count < 2)
            {
                return;
            }
            // The band: strips across the width, one per metre along and across, on the ground.
            var d = Dense(c);
            int across = Math.Max(2, (int)MathF.Ceiling(Path.Width));
            var left = Offset(d, half);
            for (int i = 1; i < d.Count; i++)
            {
                Vector2 na = (left[i - 1] - d[i - 1]) / MathF.Max(half, 1e-6f), nb = (left[i] - d[i]) / MathF.Max(half, 1e-6f);
                for (int k = 0; k < across; k++)
                {
                    float u0 = -half + Path.Width * k / across, u1 = -half + Path.Width * (k + 1) / across;
                    Vector3 p00 = V(d[i - 1] + na * u0, 0.25f), p01 = V(d[i - 1] + na * u1, 0.25f), p10 = V(d[i] + nb * u0, 0.25f), p11 = V(d[i] + nb * u1, 0.25f);
                    foreach (var v in new[] { p00, p01, p11, p00, p11, p10 })
                    {
                        band.AddRange([v.X, v.Y, v.Z]);
                    }
                }
            }
            Ribbon(edges, Offset(d, half), thick, 0.35f);
            Ribbon(edges, Offset(d, -half), thick, 0.35f);
            if (Path.Soft > 0.01f)
            {
                Ribbon(soft, Offset(d, outer), thin, 0.3f);
                Ribbon(soft, Offset(d, -outer), thin, 0.3f);
            }
            Ribbon(line, c, thick, 0.45f);
        }
        Stretch([.. curve.Select(c => c.P)]);
        // The cursor: the width and soft edge a point there brings, and the stretch from the last point.
        var preview = new List<float>();
        var previewSoft = new List<float>();
        if (_hover is { } h && !Path.Drawing)
        {
            var at = new Vector2(h.X, h.Z);
            Ribbon(preview, Circle(at, half), thick, 0.35f);
            if (Path.Soft > 0.01f)
            {
                Ribbon(previewSoft, Circle(at, outer), thin, 0.3f);
            }
            if (pts.Count > 0 && Vector2.Distance(pts[^1], at) > 0.5f)
            {
                var next = new List<Vector2> { pts[^1], at };
                Ribbon(preview, Offset(Dense(next), half), thin, 0.35f);
                Ribbon(preview, Offset(Dense(next), -half), thin, 0.35f);
                Ribbon(preview, next, thin, 0.45f);
            }
        }
        // Each point: a small square that keeps its size on screen.
        var dots = new List<float>();
        void Seg(List<float> to, Vector3 a, Vector3 b) => to.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
        foreach (var p in pts)
        {
            var c = V(p, 0.6f);
            float r = Marker(dots, c);
            Seg(dots, c - new Vector3(0, r, 0), c + new Vector3(0, r, 0));
        }
        DrawLines(ref _pathBandVao, ref _pathBandVbo, [.. band], vp, new Vector4(1, 0.45f, 0.35f, 0.16f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathSoftVao, ref _pathSoftVbo, [.. soft], vp, new Vector4(1, 0.8f, 0.7f, 0.55f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathEdgeVao, ref _pathEdgeVbo, [.. edges], vp, new Vector4(1, 0.75f, 0.65f, 0.95f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathVao, ref _pathVbo, [.. line], vp, new Vector4(1, 0.23f, 0.23f, 1), PrimitiveType.Triangles);
        DrawLines(ref _pathPreviewSoftVao, ref _pathPreviewSoftVbo, [.. previewSoft], vp, new Vector4(1, 1, 1, 0.5f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathPreviewVao, ref _pathPreviewVbo, [.. preview], vp, new Vector4(1, 1, 1, 0.9f), PrimitiveType.Triangles, blend: true);
        DrawLines(ref _pathDotVao, ref _pathDotVbo, [.. dots], vp, new Vector4(1, 0.69f, 0.63f, 1));
    }
}

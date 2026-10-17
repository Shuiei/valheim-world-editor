using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The lines the overlays and the tools are drawn with: strips for the line segments, and the line shader's draw.
public sealed partial class GlView
{
    /// <summary>
    /// Line segments (pairs of points) as thin strips facing the eye, <paramref name="width"/> times a tool line's
    /// width: they stay whole at any resolution and angle, where 1-pixel lines broke into dashes.
    /// </summary>
    private float[] Thicken(float[] seg, float width)
    {
        float[] o = new float[seg.Length / 6 * 18];
        int n = 0;
        for (int i = 0; i + 5 < seg.Length; i += 6)
        {
            var a = new Vector3(seg[i], seg[i + 1], seg[i + 2]);
            var b = new Vector3(seg[i + 3], seg[i + 4], seg[i + 5]);
            Vector3 d = b - a;
            if (d.LengthSquared() < 1e-12f)
            {
                continue;
            }
            var side = Vector3.Cross(d, (a + b) / 2 - _lastEye);
            if (side.LengthSquared() < 1e-12f)
            {
                side = Vector3.Cross(d, Vector3.UnitY);
            }
            if (side.LengthSquared() < 1e-12f)
            {
                side = Vector3.UnitX;
            }
            side = Vector3.Normalize(side);
            Vector3 sa = side * (Vector3.Distance(_lastEye, a) * _linePx * width), sb = side * (Vector3.Distance(_lastEye, b) * _linePx * width);
            foreach (var v in new[] { a - sa, a + sa, b + sb, a - sa, b + sb, b - sb })
            {
                o[n++] = v.X;
                o[n++] = v.Y;
                o[n++] = v.Z;
            }
        }
        return n == o.Length ? o : o[..n];
    }

    /// <summary>
    /// Lines (pairs of view-space points) drawn over everything in one colour, refilled every call;
    /// <paramref name="vao"/> and <paramref name="vbo"/> are made at the first.
    /// </summary>
    /// <param name="vao">The vertex array kept for these lines (0 until made).</param>
    /// <param name="vbo">Its buffer (0 until made).</param>
    /// <param name="data">The points, three floats each.</param>
    /// <param name="vp">The camera's view × projection.</param>
    /// <param name="color">The colour, with alpha (used when <paramref name="blend"/>).</param>
    /// <param name="kind">Lines, or triangles as they are.</param>
    /// <param name="blend">See-through (alpha).</param>
    /// <param name="width">For line segments, times a tool line's width (0: 1-pixel lines).</param>
    private unsafe void DrawLines(ref uint vao, ref uint vbo, float[] data, Matrix4x4 vp, Vector4 color, PrimitiveType kind = PrimitiveType.Lines, bool blend = false, float width = 1)
    {
        if (kind == PrimitiveType.Lines && width > 0)
        {
            data = Thicken(data, width);
            kind = PrimitiveType.Triangles;
        }
        if (data.Length == 0)
        {
            return;
        }
        if (vao == 0)
        {
            vao = _own.VertexArray();
            vbo = _own.Buffer();
            SetUpPoints(vao, vbo);
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.DynamicDraw);
        }
        _gl.Disable(EnableCap.DepthTest);
        _gl.Uniform4(UseLineProgram(vp), color.X, color.Y, color.Z, color.W);
        _gl.BindVertexArray(vao);
        if (blend)
        {
            // See-through (alpha), on both sides (ribbons on slopes may face away).
            GlBlend.Over(_gl);
        }
        bool cull = _gl.IsEnabled(EnableCap.CullFace);
        _gl.Disable(EnableCap.CullFace);
        _gl.DrawArrays(kind, 0, (uint)(data.Length / 3));
        if (cull)
        {
            _gl.Enable(EnableCap.CullFace);
        }
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
    }
    /// <summary>
    /// A line through points on the ground as line pairs, each stretch cut about every metre so it follows hills.
    /// </summary>
    /// <param name="into">Where the pairs go.</param>
    /// <param name="s">The area.</param>
    /// <param name="pts">The points, grid points once <paramref name="offset"/> is taken off.</param>
    /// <param name="lift">Metres above the ground.</param>
    /// <param name="closed">Whether the last point joins the first.</param>
    /// <param name="offset">Taken off each cut point to make it a grid point (world points: the area's corner).</param>
    private static void GroundPolyline(List<float> into, WorldScene s, IReadOnlyList<Vector2> pts, float lift, bool closed, Vector2 offset = default)
    {
        int n = closed ? pts.Count : pts.Count - 1;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b)));
            for (int k = 0; k < steps; k++)
            {
                Vector2 g = Vector2.Lerp(a, b, k / (float)steps) - offset, h = Vector2.Lerp(a, b, (k + 1) / (float)steps) - offset;
                var p = OnGround(s, g.X, g.Y, lift);
                var q = OnGround(s, h.X, h.Y, lift);
                into.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
            }
        }
    }

    /// <summary>
    /// Sets a vertex array up to read its points from a buffer: the buffer bound, and position attribute 0 of three
    /// floats a point (12 bytes).
    /// </summary>
    /// <param name="vao">The vertex array.</param>
    /// <param name="vbo">The buffer of the points.</param>
    private unsafe void SetUpPoints(uint vao, uint vbo)
    {
        _gl.BindVertexArray(vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
    }
}

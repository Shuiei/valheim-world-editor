using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The overlays (zone borders, wards, flattened places…) and the ground's measuring colours, and the tool lines
// every overlay is drawn with.
public sealed partial class GlView
{
    // ---- Overlays (zone borders at first) and the ground's measuring colours (game look only).
    /// <summary>Which overlays are drawn, by layer: the zone borders at first.</summary>
    private readonly bool[] _overlay = [.. Overlays.All.Select(l => l == Overlays.Layer.Borders)];
    /// <summary>Whether an overlay is drawn.</summary>
    public bool IsOverlayShown(Overlays.Layer l) => _overlay[(int)l];
    /// <summary>Shows or hides an overlay (Flatten takes its edge along).</summary>
    public void SetOverlay(Overlays.Layer l, bool on)
    {
        _overlay[(int)l] = on;
        if (l == Overlays.Layer.Flatten)
        {
            _overlay[(int)Overlays.Layer.FlattenEdge] = on;
        }
        Wake();
    }
    /// <summary>Backs <c>SlopeColours</c>.</summary>
    private volatile bool _slope;

    /// <summary>Ground coloured by slope (game look only).</summary>
    public bool SlopeColours { get => _slope; set { _slope = value; Wake(); } }
    /// <summary>The height between contour lines on the ground, in metres; 0 for none (game look only).</summary>
    public float ContourStep { get; set { field = value; Wake(); } }
    /// <summary>The red on ground at the game's ±8 m limit can be hidden (View: Limit marks).</summary>
    private volatile bool _limit = true;
    /// <summary>The red on ground at the game's ±8 m limit (View: Limit marks).</summary>
    public bool LimitMarks { get => _limit; set { _limit = value; Wake(); } }
    /// <summary>How many wards, crafting stations, flattened places and locations the overlays show.</summary>
    public event Action<Overlays.Built>? OverlaysBuilt;
    /// <summary>Each overlay's lines on the graphics card.</summary>
    private readonly Dictionary<Overlays.Layer, (uint Vao, int Count, uint Vbo)> _overlayGl = [];
    /// <summary>
    /// The overlays wait for the ground only (not for objects, which show at once): built again 200 ms
    /// after the last time at the most.
    /// </summary>
    private bool _overlaysGroundOnly;
    /// <summary>When the overlays were last built, on the view's clock.</summary>
    private long _overlaysBuiltAt;

    /// <summary>A box's twelve edges, as pairs of corners (corner k: bit 0 x, bit 1 y, bit 2 z, set: the high side).</summary>
    private static readonly (int A, int B)[] BoxEdges = [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)];

    /// <summary>Half a tool line's width per metre from the eye (set each frame from the view's size).</summary>
    private float _linePx = 0.0015f;

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
            _gl.BindVertexArray(vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
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
    /// A small flat square round a point as line pairs, its size a fixed share of the distance to the eye.
    /// </summary>
    /// <param name="into">Where the pairs go.</param>
    /// <param name="c">The point, in view space.</param>
    /// <returns>Its half width.</returns>
    private float Marker(List<float> into, Vector3 c)
    {
        float r = Vector3.Distance(_lastEye, c) * 0.008f;
        Vector3[] k = [c + new Vector3(-r, 0, -r), c + new Vector3(r, 0, -r), c + new Vector3(r, 0, r), c + new Vector3(-r, 0, r)];
        for (int j = 0; j < 4; j++)
        {
            into.AddRange([k[j].X, k[j].Y, k[j].Z, k[(j + 1) % 4].X, k[(j + 1) % 4].Y, k[(j + 1) % 4].Z]);
        }
        return r;
    }

    /// <summary>The overlays' lines to the graphics card, each layer's buffer filled again.</summary>
    private unsafe void UploadOverlays(Overlays.Built o)
    {
        foreach (var (layer, data) in o.Lines)
        {
            // Built again when objects change: the same buffers are filled again.
            if (!_overlayGl.TryGetValue(layer, out var old))
            {
                old = (_own.VertexArray(), 0, _own.Buffer());
                _gl.BindVertexArray(old.Vao);
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, old.Vbo);
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
            }
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, old.Vbo);
            fixed (float* p = data)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.DynamicDraw);
            }
            _overlayGl[layer] = (old.Vao, data.Length / 3, old.Vbo);
        }
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// The overlays that are on; each layer's style says whether it is hidden behind what stands in front of it
    /// or shows through (the rings).
    /// </summary>
    private unsafe void DrawOverlays(Matrix4x4 vp)
    {
        int uColor = UseLineProgram(vp);
        GlBlend.Over(_gl);
        _gl.DepthMask(false);
        foreach (var (layer, (vao, count, _)) in _overlayGl)
        {
            if (!_overlay[(int)layer] || count == 0)
            {
                continue;
            }
            var (c, hidden) = Overlays.Style(layer);
            if (hidden)
            {
                _gl.Enable(EnableCap.DepthTest);
            }
            else
            {
                // Rings show through what stands on them, like the web editor's.
                _gl.Disable(EnableCap.DepthTest);
            }
            _gl.Uniform4(uColor, c.X, c.Y, c.Z, c.W);
            _gl.BindVertexArray(vao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)count);
        }
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
    }

    /// <summary>
    /// Puts the line shader to use (made at first use) with this view × projection.
    /// </summary>
    /// <returns>Its colour uniform's location.</returns>
    private unsafe int UseLineProgram(Matrix4x4 viewProj)
    {
        if (_lineProg == 0)
        {
            _lineProg = Program(Shaders.LineVs, Shaders.LineFs);
        }
        _gl.UseProgram(_lineProg);
        _gl.UniformMatrix4(_u.At(_lineProg, "uViewProj"), 1, false, (float*)&viewProj);
        return _u.At(_lineProg, "uColor");
    }
}

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

    /// <summary>A box's corner k (bit 0 x, bit 1 y, bit 2 z, set: the high side), as <see cref="BoxEdges"/> numbers them.</summary>
    /// <param name="b">The box.</param>
    /// <param name="k">The corner, 0 to 7.</param>
    internal static Vector3 Corner((Vector3 Min, Vector3 Max) b, int k) =>
        new((k & 1) == 0 ? b.Min.X : b.Max.X, (k & 2) == 0 ? b.Min.Y : b.Max.Y, (k & 4) == 0 ? b.Min.Z : b.Max.Z);

    /// <summary>Half a tool line's width per metre from the eye (set each frame from the view's size).</summary>
    private float _linePx = 0.0015f;

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
                SetUpPoints(old.Vao, old.Vbo);
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

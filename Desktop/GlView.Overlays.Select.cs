using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The Select tool's marks: the move handles over the selection and the lasso.
public sealed partial class GlView
{
    /// <summary>
    /// Where the Select tool's handles go: the middle of the selection (following a move in progress), in
    /// view space, and their scale (they keep their size on screen). Null when nothing is selected.
    /// </summary>
    internal (Vector3 Centre, float Scale)? GizmoAt()
    {
        var s = Scene;
        if (s == null || !SelectMode || Tool != null)
        {
            return null;
        }
        var previews = _previews;
        Vector3 sum = Vector3.Zero;
        int n = 0;
        lock (s.Things)
        {
            foreach (int i in Selected)
            {
                var t = previews.TryGetValue(i, out var p) ? p : s.Things[i];
                if (t.Gone)
                {
                    continue;
                }
                sum += ToView(s, t.Position);
                n++;
            }
        }
        if (n == 0)
        {
            return null;
        }
        var c = sum / n;
        return (c, Vector3.Distance(_lastEye, c) * 0.07f);
    }

    /// <summary>Each move handle's triangles on the graphics card, made at first use.</summary>
    private readonly Dictionary<Gizmo.Handle, (uint Vao, int Count)> _gizmoGl = [];
    /// <summary>
    /// The Select tool's move handles over the selection, over everything; the one under the pointer lit.
    /// </summary>
    private unsafe void DrawGizmo(WorldScene s, Matrix4x4 vp)
    {
        if (GizmoAt() is not var (c, scale))
        {
            return;
        }
        if (_gizmoGl.Count == 0)
        {
            foreach (var h in Enum.GetValues<Gizmo.Handle>())
            {
                float[] mesh = Gizmo.Mesh(h);
                uint vao = _own.VertexArray(), vbo = _own.Buffer();
                _gl.BindVertexArray(vao);
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
                fixed (float* p = mesh)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(mesh.Length * 4), p, BufferUsageARB.StaticDraw);
                }
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
                _gizmoGl[h] = (vao, mesh.Length / 3);
            }
        }
        // The unit handles placed and scaled by the matrix (the line shader only knows view × projection).
        var m = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(c) * vp;
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        GlBlend.Over(_gl);
        int uColor = UseLineProgram(m);
        var hot = SelectTool.HotHandle;
        foreach (var (h, (vao, count)) in _gizmoGl)
        {
            var col = Gizmo.Color(h, hot == h);
            _gl.Uniform4(uColor, col.X, col.Y, col.Z, col.W);
            _gl.BindVertexArray(vao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)count);
        }
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
    }

    /// <summary>The lasso changed: its lines are made again at the next frame.</summary>
    private volatile bool _lassoDirty;
    /// <summary>Tells the view the Select tool's lasso changed.</summary>
    internal void LassoChanged()
    {
        _lassoDirty = true;
        Wake();
    }

    /// <summary>The lasso's lines on the graphics card.</summary>
    private uint _lassoVao, _lassoVbo;
    /// <summary>The zone being drawn with the Select tool, on the ground.</summary>
    private unsafe void DrawLasso(WorldScene s, Matrix4x4 vp)
    {
        if (_lassoDirty)
        {
            _lassoDirty = false;
            var pts = SelectTool.Lasso?.ToList();
            var data = new List<float>();
            if (pts is { Count: > 1 })
            {
                pts.Add(pts[0]);
                // World points: the area's corner taken off after cutting, as grid points.
                GroundPolyline(data, s, pts, 0.3f, closed: false, new Vector2(s.OriginX, s.OriginZ));
            }
            _lassoSegs = [.. data];
        }
        DrawLines(ref _lassoVao, ref _lassoVbo, _lassoSegs, vp, new Vector4(1, 0.76f, 0.29f, 1));
    }

    /// <summary>The lasso's lines, made again when it changes (<c>LassoChanged</c>).</summary>
    private float[] _lassoSegs = [];
}

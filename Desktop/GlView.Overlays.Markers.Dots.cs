using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The new objects' green dots: the point list on the graphics card, rebuilt when the things change.
public sealed partial class GlView
{
    /// <summary>The new objects' markers on the graphics card, and their program.</summary>
    private uint _markVao, _markVbo, _markProg;
    /// <summary>How many markers the buffer holds.</summary>
    private int _markCount;
    /// <summary>
    /// The markers are found and sent to the graphics card again at the next frame (things changed, a kind shown or
    /// hidden, a save or apply); otherwise the buffer is drawn as it is: a large area can hold millions of new objects.
    /// </summary>
    private volatile bool _marksDirty = true;

    /// <summary>
    /// The new objects' green dots (<c>NewMarkers</c>), a few pixels across at any distance (5 at 1080p, 9 at 4K):
    /// one point each, kept on the graphics card until they change.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="vp">The camera's view and projection.</param>
    /// <param name="height">The drawing's height in pixels.</param>
    private unsafe void DrawNewMarkers(WorldScene s, Matrix4x4 vp, int height)
    {
        if (_markProg == 0)
        {
            _markProg = Program(Shaders.MarkVs, Shaders.MarkFs);
            _markVao = _own.VertexArray();
            _markVbo = _own.Buffer();
            SetUpPoints(_markVao, _markVbo);
        }
        if (_marksDirty)
        {
            _marksDirty = false;
            var marks = NewMarkers(s);
            _markCount = marks.Count;
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _markVbo);
            fixed (Vector3* p = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(marks))
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(marks.Count * 12), p, BufferUsageARB.StaticDraw);
            }
        }
        if (_markCount == 0)
        {
            return;
        }
        if (!_es)
        {
            // Desktop OpenGL takes the size from the shader only when asked (OpenGL ES always does).
            _gl.Enable(EnableCap.ProgramPointSize);
        }
        _gl.Disable(EnableCap.DepthTest);
        // The dot's soft edge blends over what is under it.
        GlBlend.Over(_gl);
        _gl.UseProgram(_markProg);
        _gl.UniformMatrix4(_u.At(_markProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.Uniform4(_u.At(_markProg, "uColor"), 0x5d / 255f, 1, 0x8a / 255f, 1);
        // A small mark, not a blob: 0.43% of the height (5 pixels at 1080p, 9 at 4K), never under 3.
        _gl.Uniform1(_u.At(_markProg, "uSize"), MathF.Max(3, 0.0043f * height));
        _gl.BindVertexArray(_markVao);
        _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_markCount);
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
    }
}

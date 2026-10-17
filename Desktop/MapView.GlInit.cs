using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// The map's OpenGL setup: the programs, buffers and vertex arrays, and their release with the context.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// Compiles the map, line and fill programs and makes their buffers.
    /// </summary>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
        _own = new GlObjects(_gl);
        _u = new GlUniforms(_gl);
        _es = GlVersion.Type == GlProfileType.OpenGLES;
        // In the log, as the 3D view's: what draws the map.
        Options.Say($"map: OpenGL {(_es ? "ES " : "")}{GlVersion.Major}.{GlVersion.Minor}: {_gl.GetStringS(StringName.Renderer)}");
        _prog = Program(MapShader.Vertex, MapShader.Fragment);
        _vao = _own.VertexArray();
        _gl.BindVertexArray(_vao);
        uint vbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        float[] tri = [-1, -1, 3, -1, -1, 3];
        unsafe
        {
            fixed (float* p = tri)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(tri.Length * 4), p, BufferUsageARB.StaticDraw);
            }
            uint loc = (uint)_gl.GetAttribLocation(_prog, "pos");
            _gl.EnableVertexAttribArray(loc);
            _gl.VertexAttribPointer(loc, 2, VertexAttribPointerType.Float, false, 0, (void*)0);
        }
        _lineProg = Program(Shaders.LineVs, Shaders.LineFs);
        _lineVao = _own.VertexArray();
        _lineVbo = _own.Buffer();
        _gl.BindVertexArray(_lineVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
        _gl.EnableVertexAttribArray(0);
        unsafe
        {
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
        }
        _fillProg = Program(FillVs, FillFs);
        _fillVao = _own.VertexArray();
        _fillVbo = _own.Buffer();
        _gl.BindVertexArray(_fillVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _fillVbo);
        _gl.EnableVertexAttribArray(0);
        _gl.EnableVertexAttribArray(1);
        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 24, (void*)0);
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 24, (void*)8);
        }
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Shapes in world metres with a colour per corner (buildings, zone fills, pins, players).
    /// </summary>
    private const string FillVs = """
		layout(location = 0) in vec2 aPos;
		layout(location = 1) in vec4 aCol;
		uniform vec4 uView;
		out vec4 vCol;
		void main() { gl_Position = vec4((aPos - uView.xy) * uView.zw, 0.0, 1.0); vCol = aCol; }
		""";

    /// <summary>
    /// The fill program's fragment shader: the corner colours as they are.
    /// </summary>
    private const string FillFs = """
		in vec4 vCol;
		out vec4 frag;
		void main() { frag = vCol; }
		""";

    /// <summary>
    /// The map left the window (the 3D editor is shown): its OpenGL context goes. Its textures, buffers and programs
    /// are shared with Avalonia's own context and would outlive it: they are deleted, then uploaded again into the next
    /// context.
    /// </summary>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _own.DeleteAll();
        _tex.Clear();
        _texturesLoaded = false;
        _globalShown = false;
        _detailShown = null;
    }

    /// <summary>
    /// A linked program from vertex and fragment source, with the version line for this context put in front.
    /// </summary>
    /// <returns>The program, owned by <c>_own</c>.</returns>
    private uint Program(string vs, string fs)
    {
        string head = _es ? "#version 300 es\nprecision highp float;\nprecision highp int;\n" : "#version 330 core\n";
        return _own.Program(GlProgramLinker.Link(_gl, head, vs, fs));
    }
}

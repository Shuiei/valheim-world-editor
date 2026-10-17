using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The shader programs: each compiled and linked, and the program's error with its log.
public sealed partial class GlView
{
    /// <summary>A shader program owned by this view (deleted with its context).</summary>
    private uint Program(string vs, string fs) => _own.Program(Link(vs, fs));

    /// <summary>A program not yet anyone's (the game look keeps its own).</summary>
    private uint Link(string vs, string fs)
    {
        // GLSL ES gives samplers low precision by default: heights read from a texture (the water's uHeight) need
        // high.
        string head = _es ? "#version 300 es\nprecision highp float;\nprecision highp int;\nprecision highp sampler2D;\n" : "#version 330 core\n";
        uint Compile(ShaderType t, string src)
        {
            uint s = _gl.CreateShader(t);
            _gl.ShaderSource(s, head + src);
            _gl.CompileShader(s);
            _gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0)
            {
                // What failed goes with the error (a failing game shader would leak them each time it is tried).
                string log = _gl.GetShaderInfoLog(s);
                _gl.DeleteShader(s);
                throw new InvalidOperationException($"{t}: {log}");
            }
            return s;
        }
        uint v = Compile(ShaderType.VertexShader, vs), f;
        try
        {
            f = Compile(ShaderType.FragmentShader, fs);
        }
        catch (InvalidOperationException)
        {
            _gl.DeleteShader(v);
            throw;
        }
        uint p = _gl.CreateProgram();
        _gl.AttachShader(p, v);
        _gl.AttachShader(p, f);
        _gl.LinkProgram(p);
        _gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = _gl.GetProgramInfoLog(p);
            _gl.DeleteProgram(p);
            _gl.DeleteShader(v);
            _gl.DeleteShader(f);
            throw new InvalidOperationException(log);
        }
        _gl.DeleteShader(v);
        _gl.DeleteShader(f);
        return p;
    }
}

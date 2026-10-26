using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// Compiles and links a shader program from its vertex and fragment source. The caller puts the version line (and any
/// precision lines) in <c>head</c>, since each view's context needs its own. A shader that fails to compile, or a link
/// that fails, is deleted with what it left behind, and its log goes with the error.
/// </summary>
internal static class GlProgramLinker
{
    /// <summary>
    /// A linked program, not yet owned by anyone (the caller hands it to its <see cref="GlObjects"/>).
    /// </summary>
    /// <param name="gl">The context's OpenGL.</param>
    /// <param name="head">The text put in front of both sources, such as the <c>#version</c> line.</param>
    /// <param name="vs">The vertex shader source.</param>
    /// <param name="fs">The fragment shader source.</param>
    /// <returns>The program's name.</returns>
    /// <exception cref="InvalidOperationException">A shader or the link failed; the message has the log.</exception>
    public static uint Link(GL gl, string head, string vs, string fs)
    {
        uint Compile(ShaderType t, string src)
        {
            uint s = gl.CreateShader(t);
            gl.ShaderSource(s, head + src);
            gl.CompileShader(s);
            gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0)
            {
                // What failed goes with the error (a failing game shader would leak them each time it is tried).
                string log = gl.GetShaderInfoLog(s);
                gl.DeleteShader(s);
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
            gl.DeleteShader(v);
            throw;
        }
        uint p = gl.CreateProgram();
        gl.AttachShader(p, v);
        gl.AttachShader(p, f);
        gl.LinkProgram(p);
        gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = gl.GetProgramInfoLog(p);
            gl.DeleteProgram(p);
            gl.DeleteShader(v);
            gl.DeleteShader(f);
            throw new InvalidOperationException(log);
        }
        gl.DeleteShader(v);
        gl.DeleteShader(f);
        return p;
    }
}

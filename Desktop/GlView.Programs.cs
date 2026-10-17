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
        return GlProgramLinker.Link(_gl, head, vs, fs);
    }
}

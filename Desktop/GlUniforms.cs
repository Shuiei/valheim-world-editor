using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// Uniform locations of one OpenGL context's programs, each asked of the driver once: GetUniformLocation sends the
/// name across and asks the driver at every call, and the views set dozens of uniforms every frame. Made again with
/// the programs (a new context, another area), so a deleted program's number never finds old locations.
/// </summary>
/// <param name="gl">The context's OpenGL.</param>
internal sealed class GlUniforms(GL gl)
{
    /// <summary>The locations asked so far, by program and name.</summary>
    private readonly Dictionary<(uint Program, string Name), int> _known = [];

    /// <summary>A uniform's location in a program (-1: the program has no such uniform in use).</summary>
    public int At(uint program, string name)
    {
        if (!_known.TryGetValue((program, name), out int at))
        {
            _known[(program, name)] = at = gl.GetUniformLocation(program, name);
        }
        return at;
    }
}

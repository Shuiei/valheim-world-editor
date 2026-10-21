// Adapting the terrain's OpenGL fragment variant to WebGL 2 (see GameShader).

namespace TerrainEditor.App;

public static partial class GameShader
{
    /// <summary>
    /// Adapts a fragment variant by its declarations only (the shader's own statements are left as they are): keeps the
    /// declarations from the first uniform to the end of main, drops the location qualifiers, makes the G-buffer
    /// outputs and the zone corners' ocean depths (<c>_depth</c>) globals the editor's main sets and reads, and renames
    /// main to <c>valheimGBuffer</c>. Throws <see cref="InvalidDataException"/> when an expected declaration is missing.
    /// </summary>
    private static string Convert(string src)
    {
        static InvalidDataException Changed(string what) => new($"The terrain shader changed ({what}); the editor needs an update.");
        var first = FirstUniform().Match(src);
        if (!first.Success)
        {
            throw Changed("uniforms");
        }
        string body = src[first.Index..];
        body = body[..(body.LastIndexOf('}') + 1)];
        body = Location().Replace(body, "uniform ");
        body = Output().Replace(body, "vec4 $1;");
        if (DepthUniform().Count(body) != 1)
        {
            throw Changed("ocean depth");
        }
        body = DepthUniform().Replace(body, "float _depth[4];");
        if (!MaskSampler().IsMatch(body))
        {
            throw Changed("paint mask");
        }
        if (!MaskCoordinates().IsMatch(body))
        {
            throw Changed("texcoords");
        }
        int at = body.IndexOf("void main()", StringComparison.Ordinal);
        if (at >= 0)
        {
            body = body.Remove(at, "void main()".Length).Insert(at, "void valheimGBuffer()");
        }
        return GlHeader.Replace("\r\n", "\n", StringComparison.Ordinal) + body + "\n";
    }
}

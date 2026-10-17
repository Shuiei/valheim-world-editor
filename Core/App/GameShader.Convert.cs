// Adapting the terrain's OpenGL fragment variant to WebGL 2 (see GameShader).

namespace TerrainEditor.App;

public static partial class GameShader
{
    /// <summary>
    /// Adapts a fragment variant: keeps the uniforms to the end of main, drops the location qualifiers, makes the
    /// G-buffer outputs globals, takes the ocean depth and paint mask UV from varyings and renames main to
    /// <c>valheimGBuffer</c>. Throws <see cref="InvalidDataException"/> when an expected piece is missing.
    /// </summary>
    private static string Convert(string src)
    {
        static InvalidDataException Changed(string what) => new($"The terrain shader changed ({what}); the editor needs an update.");
        int start = src.IndexOf("uniform \tvec4 _Time;", StringComparison.Ordinal);
        if (start < 0)
        {
            throw Changed("uniforms");
        }
        string body = src[start..];
        body = body[..(body.LastIndexOf('}') + 1)];
        body = Location().Replace(body, "uniform ");
        body = Output().Replace(body, "vec4 $1;");
        body = body.Replace("uniform \tfloat _depth[4];\n", "", StringComparison.Ordinal);
        string depth = DepthBlock.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (Count(body, depth) != 1)
        {
            throw Changed("depth block not found");
        }
        body = body.Replace(depth, "    u_xlat66 = vs_depth;", StringComparison.Ordinal);
        if (Count(body, "texture(_ClearedMaskTex, vs_TEXCOORD0.xy)") != 1)
        {
            throw Changed("paint mask");
        }
        body = body.Replace("texture(_ClearedMaskTex, vs_TEXCOORD0.xy)", "texture(_ClearedMaskTex, vs_maskUV)", StringComparison.Ordinal);
        if (body.Replace("in  vec2 vs_TEXCOORD0;", "", StringComparison.Ordinal).Contains("vs_TEXCOORD0", StringComparison.Ordinal))
        {
            throw Changed("texcoords");
        }
        body = body.Replace("in  vec2 vs_TEXCOORD0;", "in  float vs_depth;\nin  vec2 vs_maskUV;", StringComparison.Ordinal);
        int at = body.IndexOf("void main()", StringComparison.Ordinal);
        if (at >= 0)
        {
            body = body.Remove(at, "void main()".Length).Insert(at, "void valheimGBuffer()");
        }
        return GlHeader.Replace("\r\n", "\n", StringComparison.Ordinal) + body + "\n";
    }

    /// <summary>Non-overlapping occurrences of <paramref name="what"/> in <paramref name="s"/> (ordinal).</summary>
    private static int Count(string s, string what)
    {
        int n = 0;
        for (int i = s.IndexOf(what, StringComparison.Ordinal); i >= 0; i = s.IndexOf(what, i + what.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }
}

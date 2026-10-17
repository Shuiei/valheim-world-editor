using System.Text;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

public static partial class TerrainShader
{
    /// <summary>
    /// The SPIRV-Cross output made into what GameLookGl links with the editor's vertex shader. Throws
    /// InvalidDataException when an expected piece is missing (the game's shader changed).
    /// </summary>
    internal static string Adapt(string src)
    {
        static string Need(string s, string what, string why)
        {
            if (!s.Contains(what, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The terrain shader changed ({why}); the editor needs an update.");
            }
            return s;
        }
        string body = src.Replace("\r\n", "\n");
        body = VersionOrPrecision().Replace(body, "");
        // The uniform buffer as plain uniforms: struct X { members }; uniform X _Globals;
        var block = UniformStruct().Match(body);
        if (!block.Success)
        {
            throw new InvalidDataException("The terrain shader changed (its uniforms); the editor needs an update.");
        }
        var plain = new StringBuilder();
        foreach (string line in block.Groups["members"].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // The ocean depth arrives as a varying (vs_depth): _depth = (1, 1, 0, 0) makes the shader's
            // blend of the zone corners' depths, by vs_TEXCOORD0 = (0, depth), the depth itself.
            plain.Append(line == "float _depth[4];" ? "const float _depth[4] = float[4](1.0, 1.0, 0.0, 0.0);\n" : "uniform " + line + "\n");
        }
        body = body.Remove(block.Index, block.Length).Insert(block.Index, plain.ToString());
        body = Need(body, "_Globals.", "uniform names").Replace("_Globals.", "");
        Need(body, "const float _depth[4]", "ocean depth");
        // The paint mask's coordinates, then the depth, in place of the shader's vs_TEXCOORD0.
        body = Need(body, "texture(_ClearedMaskTex, vs_TEXCOORD0)", "paint mask").Replace("texture(_ClearedMaskTex, vs_TEXCOORD0)", "texture(_ClearedMaskTex, vs_maskUV)");
        body = Need(body, "in vec2 vs_TEXCOORD0;", "texcoords").Replace("in vec2 vs_TEXCOORD0;", "in float vs_depth;\nin vec2 vs_maskUV;");
        body = Regex.Replace(body, @"\bvs_TEXCOORD0\b", "vec2(0.0, vs_depth)");
        // The G-buffer targets as globals for the editor's main() to light.
        body = Outputs().Replace(body, "vec4 $1;");
        for (int i = 0; i < 4; i++)
        {
            Need(body, $"vec4 SV_Target{i};", "G-buffer targets");
        }
        // Fragment inputs carry no location in GLSL ES 3.0 (they link by name).
        body = InputLocation().Replace(body, "in ");
        body = Need(body, "void main()", "entry point");
        int at = body.IndexOf("void main()", StringComparison.Ordinal);
        body = body.Remove(at, "void main()".Length).Insert(at, "void valheimGBuffer()");
        return Header + body.Trim() + "\n";
    }

    /// <summary>SPIRV-Cross's #version and precision lines, replaced by <see cref="Header"/>.</summary>
    [GeneratedRegex(@"^(#version[^\n]*|precision [^\n]*;)\n", RegexOptions.Multiline)]
    private static partial Regex VersionOrPrecision();

    /// <summary>The _Globals uniform buffer as SPIRV-Cross writes it: a struct and one uniform of it.</summary>
    [GeneratedRegex(@"struct (?<type>\w+)\n\{\n(?<members>[^}]*)\};\n\nuniform \k<type> _Globals;\n")]
    private static partial Regex UniformStruct();

    /// <summary>A G-buffer output declaration; group 1 is its name.</summary>
    [GeneratedRegex(@"layout\(location = \d+\) out vec4 (SV_Target\d);")]
    private static partial Regex Outputs();

    /// <summary>A fragment input's location qualifier.</summary>
    [GeneratedRegex(@"layout\(location = \d+\) in ")]
    private static partial Regex InputLocation();
}

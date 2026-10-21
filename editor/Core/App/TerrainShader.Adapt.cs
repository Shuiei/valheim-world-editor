using System.Text;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

public static partial class TerrainShader
{
    /// <summary>
    /// The SPIRV-Cross output made into what GameLookGl links with the editor's vertex shader, by its declarations only
    /// (the shader's own statements are left as they are). Throws InvalidDataException when an expected declaration is
    /// missing (the game's shader changed).
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
            // The zone corners' ocean depths: a global the editor's main sets (the area is one mesh, not a zone).
            plain.Append(line == "float _depth[4];" ? "float _depth[4];\n" : "uniform " + line + "\n");
        }
        body = body.Remove(block.Index, block.Length).Insert(block.Index, plain.ToString());
        body = Need(body, "_Globals.", "uniform names").Replace("_Globals.", "");
        Need(body, "\nfloat _depth[4];", "ocean depth");
        Need(body, "sampler2D _ClearedMaskTex;", "paint mask");
        Need(body, "in vec2 vs_TEXCOORD0;", "texcoords");
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

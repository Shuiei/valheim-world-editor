// The terrain shader's OpenGL program: its deferred pass's fragment variant, and the regular expressions that adapt it (see GameShader).

using System.Text;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

public static partial class GameShader
{
    /// <summary>
    /// The editor's GLSL fragment shader from the OpenGL core program: one fragment variant of the deferred
    /// pass that samples the terrain textures and the full paint mask, preferring four G-buffer targets and
    /// no fog, adapted by <c>Convert</c>.
    /// </summary>
    /// <param name="program">The OpenGL core program blob, GLSL source as Latin-1 text.</param>
    /// <exception cref="InvalidDataException">No usable variant, or the shader changed from what is adapted.</exception>
    public static string FromOpenGl(byte[] program)
    {
        var keep = FragmentVariants(Encoding.Latin1.GetString(program));
        string best = keep.GetValueOrDefault(("vs", true, 4, false)) ?? keep.GetValueOrDefault(("vs", true, 1, false)) ?? keep.GetValueOrDefault(("vs", true, 1, true))
            ?? throw new InvalidDataException("The terrain shader has no usable fragment variant; the editor needs an update.");
        return Convert(NotPrintable().Replace(best, ""));
    }

    /// <summary>
    /// The deferred pass's fragment variants in the program's text, each under its prefix (ds or vs), whether it samples
    /// the full paint mask, its number of G-buffer targets and whether it has fog; the first of each key is kept.
    /// </summary>
    /// <param name="d">The program's text.</param>
    private static Dictionary<(string, bool, int, bool), string> FragmentVariants(string d)
    {
        var keep = new Dictionary<(string, bool, int, bool), string>();
        int i = 0;
        while (true)
        {
            int a = d.IndexOf("#ifdef FRAGMENT", i, StringComparison.Ordinal);
            if (a < 0)
            {
                break;
            }
            int main = d.IndexOf("void main", a, StringComparison.Ordinal);
            int b = main < 0 ? -1 : d.IndexOf("#endif\n", main, StringComparison.Ordinal);
            if (b < 0)
            {
                break;
            }
            string f = d[a..b];
            i = b;
            if (!f.Contains("_DiffuseArrayTex", StringComparison.Ordinal))
            {
                continue;
            }
            string pref = f.Contains("ds_TEXCOORD0", StringComparison.Ordinal) ? "ds" : "vs";
            // The full paint mask is sampled where the variant declares it (the compiler drops what a variant does not use).
            bool full = MaskSampler().IsMatch(f);
            int outs = Targets().Matches(f).Select(m => m.Groups[1].Value).Distinct().Count();
            keep.TryAdd((pref, full, outs, f.Contains("unity_FogParams", StringComparison.Ordinal)), f);
        }
        return keep;
    }

    /// <summary>The header put before the adapted fragment shader: what it is and WebGL 2 precisions.</summary>
    private const string GlHeader = """
		// Valheim's terrain shader (Custom/Heightmap, deferred G-buffer pass), compiled by Unity for
		// OpenGL core, its declarations adapted to WebGL 2: the four G-buffer targets and the zone corners'
		// ocean depths become globals the editor's main() sets and lights. Generated; do not edit by hand.
		precision highp float;
		precision highp int;
		precision highp sampler2D;
		precision highp sampler3D;
		precision highp sampler2DArray;

		""";

    /// <summary>Assignments to a G-buffer target; group 1 is its number.</summary>
    [GeneratedRegex(@"SV_Target(\d) =")]
    private static partial Regex Targets();

    /// <summary>Characters other than tab, newline and printable ASCII (blob padding), removed.</summary>
    [GeneratedRegex(@"[^\x09\x0a\x20-\x7e]")]
    private static partial Regex NotPrintable();

    /// <summary>Unity's sampler location qualifier, which WebGL does not have.</summary>
    [GeneratedRegex(@"UNITY_LOCATION\(\d+\) uniform ")]
    private static partial Regex Location();

    /// <summary>A G-buffer output declaration; group 1 is its name.</summary>
    [GeneratedRegex(@"layout\(location = \d\) out vec4 (SV_Target\d);")]
    private static partial Regex Output();

    /// <summary>The first uniform declaration (with or without Unity's location qualifier): where the kept part starts.</summary>
    [GeneratedRegex(@"^(UNITY_LOCATION\(\d+\) )?uniform\b", RegexOptions.Multiline)]
    private static partial Regex FirstUniform();

    /// <summary>The declaration of the zone corners' ocean depths, the uniform <c>_depth</c>.</summary>
    [GeneratedRegex(@"uniform\s+float\s+_depth\[4\];")]
    private static partial Regex DepthUniform();

    /// <summary>The declaration of the paint mask sampler, <c>_ClearedMaskTex</c>.</summary>
    [GeneratedRegex(@"uniform\s+(\w+\s+)?sampler2D\s+_ClearedMaskTex;")]
    private static partial Regex MaskSampler();

    /// <summary>The declaration of the paint mask's coordinates, the input <c>vs_TEXCOORD0</c>.</summary>
    [GeneratedRegex(@"\bin\s+vec2\s+vs_TEXCOORD0;")]
    private static partial Regex MaskCoordinates();
}

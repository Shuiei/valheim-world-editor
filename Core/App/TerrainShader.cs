using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Silk.NET.SPIRV.Cross;

namespace TerrainEditor.App;

/// <summary>
/// The terrain shader from a game copy without OpenGL programs (Valheim for Windows has Direct3D 11
/// and Vulkan only): the deferred pass's Vulkan program as SPIR-V, with its names put back
/// (GameShader.FromVulkan), turned into the GLSL the OpenGL path makes (GameShader.FromOpenGl): plain
/// uniforms, the four G-buffer targets as globals, the ocean depth and the paint mask as varyings,
/// main() renamed.
/// </summary>
public static partial class TerrainShader
{
    /// <summary>The header put before the adapted shader: what it is and the precisions (all highp).</summary>
    private const string Header = """
		// Valheim's terrain shader (Custom/Heightmap, deferred G-buffer pass), compiled by Unity for
		// Vulkan, turned into GLSL by SPIRV-Cross and adapted like the OpenGL one: the four G-buffer
		// targets become globals that main() lights, the per-zone ocean depth and the paint mask arrive
		// as varyings. Generated; do not edit by hand.
		precision highp float;
		precision highp int;
		precision highp sampler2D;
		precision highp sampler3D;
		precision highp sampler2DArray;

		""";

    /// <summary>The editor's GLSL fragment shader from the named SPIR-V (bytes). Throws InvalidDataException.</summary>
    public static string FromSpirv(byte[] spirv) => Adapt(CrossCompile(spirv));

    /// <summary>
    /// SPIRV-Cross loads and runs here (the smoke tests ask, through the driver's "spirv"): the GLSL of
    /// a fragment shader that writes white.
    /// </summary>
    public static string SelfTest() => CrossCompile(WhiteFragment());

    /// <summary>A minimal SPIR-V fragment shader (bytes) whose one output is white.</summary>
    internal static byte[] WhiteFragment()
    {
        var w = new List<uint> { 0x07230203, 0x00010000, 0, 11, 0 };
        void Op(ushort op, params uint[] a)
        {
            w.Add(((uint)(a.Length + 1) << 16) | op);
            w.AddRange(a);
        }
        const uint Main = 1, Output = 2, TVoid = 3, TFn = 4, TFloat = 5, TVec4 = 6, TPtr = 7, One = 8, White = 9, Label = 10;
        Op(17, 1); // OpCapability Shader
        Op(14, 0, 1); // OpMemoryModel Logical GLSL450
        Op(15, 4, Main, 0x6E69616D, 0, Output); // OpEntryPoint Fragment %main "main" %output
        Op(16, Main, 8); // OpExecutionMode OriginUpperLeft
        Op(71, Output, 30, 0); // OpDecorate Location 0
        Op(19, TVoid);
        Op(33, TFn, TVoid);
        Op(22, TFloat, 32);
        Op(23, TVec4, TFloat, 4);
        Op(32, TPtr, 3, TVec4);
        Op(59, TPtr, Output, 3);
        Op(43, TFloat, One, 0x3F800000);
        Op(44, TVec4, White, One, One, One, One);
        Op(54, TVoid, Main, 0, TFn);
        Op(248, Label);
        Op(62, Output, White);
        Op(253);
        Op(56);
        return [.. w.SelectMany(BitConverter.GetBytes)];
    }

    /// <summary>
    /// SPIR-V to GLSL ES 3.0 (the editor's ANGLE target; desktop OpenGL takes it too), all highp, uniform
    /// buffers as plain uniforms. Throws InvalidDataException with SPIRV-Cross's error.
    /// </summary>
    internal static unsafe string CrossCompile(byte[] spirv)
    {
        if (spirv.Length < 20 || spirv.Length % 4 != 0)
        {
            throw new InvalidDataException("The terrain shader file is not SPIR-V.");
        }
        var api = Shared();
        Context* ctx;
        Check(api.ContextCreate(&ctx), null);
        try
        {
            ParsedIr* ir;
            fixed (byte* p = spirv)
            {
                Check(api.ContextParseSpirv(ctx, (uint*)p, (nuint)(spirv.Length / 4), &ir), ctx);
            }
            Compiler* compiler;
            Check(api.ContextCreateCompiler(ctx, Backend.Glsl, ir, CaptureMode.TakeOwnership, &compiler), ctx);
            CompilerOptions* options;
            Check(api.CompilerCreateCompilerOptions(compiler, &options), ctx);
            api.CompilerOptionsSetUint(options, CompilerOption.GlslVersion, 300);
            api.CompilerOptionsSetBool(options, CompilerOption.GlslES, 1);
            api.CompilerOptionsSetBool(options, CompilerOption.GlslESDefaultFloatPrecisionHighp, 1);
            api.CompilerOptionsSetBool(options, CompilerOption.GlslESDefaultIntPrecisionHighp, 1);
            api.CompilerOptionsSetBool(options, CompilerOption.GlslEmitUniformBufferAsPlainUniforms, 1);
            Check(api.CompilerInstallCompilerOptions(compiler, options), ctx);
            byte* source;
            Check(api.CompilerCompile(compiler, &source), ctx);
            return Marshal.PtrToStringUTF8((nint)source) ?? "";
        }
        finally
        {
            api.ContextDestroy(ctx);
        }

        void Check(Result r, Context* c)
        {
            if (r != Result.Success)
            {
                string? why = c == null ? null : Marshal.PtrToStringUTF8((nint)api.ContextGetLastErrorString(c));
                throw new InvalidDataException($"SPIRV-Cross could not read the terrain shader ({r}): {why}");
            }
        }
    }

    /// <summary>Guards <see cref="_cross"/>.</summary>
    private static readonly Lock CrossLock = new();

    /// <summary>SPIRV-Cross's API once loaded (<see cref="Shared"/>).</summary>
    private static Cross? _cross;

    /// <summary>
    /// SPIRV-Cross's API, loaded once for every compile (each load made a native context that was never freed); a
    /// load that fails is tried again next time.
    /// </summary>
    internal static Cross Shared()
    {
        lock (CrossLock)
        {
            return _cross ??= Api();
        }
    }

    /// <summary>
    /// SPIRV-Cross's native library: next to the program, in the folder a single-file app unpacks its
    /// native libraries into, or under runtimes/&lt;system&gt;/native (builds made without a target system,
    /// where Silk.NET's own search does not look). Else Silk.NET's own search.
    /// </summary>
    private static Cross Api()
    {
        string file = OperatingSystem.IsWindows() ? "spirv-cross.dll" : OperatingSystem.IsMacOS() ? "libspirv-cross.dylib" : "libspirv-cross.so";
        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var dirs = new List<string> { AppContext.BaseDirectory };
        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string search)
        {
            dirs.AddRange(search.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        }
        dirs.Add(Path.Combine(AppContext.BaseDirectory, "runtimes", $"{os}-{arch}", "native"));
        foreach (string d in dirs)
        {
            string path = Path.Combine(d, file);
            if (File.Exists(path))
            {
                return new Cross(Cross.CreateDefaultContext([path]));
            }
        }
        return Cross.GetApi();
    }

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
        var plain = new System.Text.StringBuilder();
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

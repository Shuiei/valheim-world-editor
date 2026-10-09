using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Silk.NET.SPIRV.Cross;

namespace TerrainEditor.App;

// The terrain shader from a game copy without OpenGL programs (Valheim for Windows has Direct3D 11
// and Vulkan only): the game-look copy writes the deferred pass's Vulkan program as SPIR-V, with its
// names put back (heightmap.frag.spv, see tools/asset-export/vulkan_shader.py); this turns it into
// the GLSL the OpenGL path makes (export_all.py convert_shader): plain uniforms, the four G-buffer
// targets as globals, the ocean depth and the paint mask as varyings, main() renamed.
public static partial class TerrainShader
{
	public const string SpirvFile = "heightmap.frag.spv";
	public const string GlslFile = "heightmap.frag.glsl";

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

	// Turns terrain/heightmap.frag.spv into heightmap.frag.glsl; true when there was one to turn.
	public static bool ConvertIn(string terrainDir)
	{
		string spv = Path.Combine(terrainDir, SpirvFile);
		if (!File.Exists(spv))
		{
			return false;
		}
		string glsl = FromSpirv(File.ReadAllBytes(spv));
		string tmp = Path.Combine(terrainDir, GlslFile + ".tmp");
		File.WriteAllText(tmp, glsl);
		File.Move(tmp, Path.Combine(terrainDir, GlslFile), overwrite: true);
		return true;
	}

	public static string FromSpirv(byte[] spirv) => Adapt(CrossCompile(spirv));

	// SPIRV-Cross loads and runs here (the smoke tests ask, through the driver's "spirv"): the GLSL of
	// a fragment shader that writes white.
	public static string SelfTest() => CrossCompile(WhiteFragment());

	internal static byte[] WhiteFragment()
	{
		var w = new List<uint> { 0x07230203, 0x00010000, 0, 11, 0 };
		void Op(ushort op, params uint[] a)
		{
			w.Add(((uint)(a.Length + 1) << 16) | op);
			w.AddRange(a);
		}
		const uint main = 1, output = 2, tVoid = 3, tFn = 4, tFloat = 5, tVec4 = 6, tPtr = 7, one = 8, white = 9, label = 10;
		Op(17, 1); // OpCapability Shader
		Op(14, 0, 1); // OpMemoryModel Logical GLSL450
		Op(15, 4, main, 0x6E69616D, 0, output); // OpEntryPoint Fragment %main "main" %output
		Op(16, main, 8); // OpExecutionMode OriginUpperLeft
		Op(71, output, 30, 0); // OpDecorate Location 0
		Op(19, tVoid);
		Op(33, tFn, tVoid);
		Op(22, tFloat, 32);
		Op(23, tVec4, tFloat, 4);
		Op(32, tPtr, 3, tVec4);
		Op(59, tPtr, output, 3);
		Op(43, tFloat, one, 0x3F800000);
		Op(44, tVec4, white, one, one, one, one);
		Op(54, tVoid, main, 0, tFn);
		Op(248, label);
		Op(62, output, white);
		Op(253);
		Op(56);
		return w.SelectMany(BitConverter.GetBytes).ToArray();
	}

	// SPIR-V to GLSL ES 3.0 (the editor's ANGLE target; desktop OpenGL takes it too), all highp.
	internal static unsafe string CrossCompile(byte[] spirv)
	{
		if (spirv.Length < 20 || spirv.Length % 4 != 0)
		{
			throw new InvalidDataException("The terrain shader file is not SPIR-V.");
		}
		var api = Api();
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

	// SPIRV-Cross's native library: next to the program, in the folder a single-file app unpacks its
	// native libraries into, or under runtimes/<system>/native (builds made without a target system,
	// where Silk.NET's own search does not look).
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
				return new Cross(Cross.CreateDefaultContext(new[] { path }));
			}
		}
		return Cross.GetApi();
	}

	// The SPIRV-Cross output made into what GameLookGl links with the editor's vertex shader.
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

	[GeneratedRegex(@"^(#version[^\n]*|precision [^\n]*;)\n", RegexOptions.Multiline)]
	private static partial Regex VersionOrPrecision();

	[GeneratedRegex(@"struct (?<type>\w+)\n\{\n(?<members>[^}]*)\};\n\nuniform \k<type> _Globals;\n")]
	private static partial Regex UniformStruct();

	[GeneratedRegex(@"layout\(location = \d+\) out vec4 (SV_Target\d);")]
	private static partial Regex Outputs();

	[GeneratedRegex(@"layout\(location = \d+\) in ")]
	private static partial Regex InputLocation();
}

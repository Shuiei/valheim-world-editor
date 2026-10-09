using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

// The terrain shader from Valheim for Windows (no OpenGL programs): its Vulkan program, as SPIR-V,
// turned into the GLSL the OpenGL path makes. The game's own shader cannot be shipped with the tests:
// a small SPIR-V module checks that SPIRV-Cross loads and runs, and a cut-down SPIRV-Cross output
// checks the changes made to it.
public class CoreTerrainShaderTests
{
	private static byte[] WhiteFragment() => TerrainShader.WhiteFragment();

	[Fact]
	public void SpirvCrossTurnsSpirvIntoGlslEs()
	{
		string glsl = TerrainShader.SelfTest();
		Assert.StartsWith("#version 300 es", glsl);
		Assert.Contains("precision highp float;", glsl);
		Assert.Matches(@"out (highp )?vec4 \w+;", glsl);
		Assert.Contains("vec4(1.0)", glsl);
		// Adapt refuses it (it is not the terrain shader), saying the game's shader changed.
		var e = Assert.Throws<InvalidDataException>(() => TerrainShader.FromSpirv(WhiteFragment()));
		Assert.Contains("The terrain shader changed", e.Message);
	}

	[Fact]
	public void SomethingElseIsNotTakenForSpirv()
	{
		Assert.Throws<InvalidDataException>(() => TerrainShader.FromSpirv(new byte[] { 1, 2, 3 }));
		var e = Assert.Throws<InvalidDataException>(() => TerrainShader.FromSpirv(new byte[64]));
		Assert.Contains("SPIRV-Cross could not read the terrain shader", e.Message);
	}

	// What SPIRV-Cross makes of the game's program, cut down to the parts that change.
	private const string CrossOutput = """
		#version 300 es
		precision highp float;
		precision highp int;

		struct _32
		{
		    vec4 _Time;
		    vec3 _WorldSpaceCameraPos;
		    float _depth[4];
		    float _Wet;
		};

		uniform _32 _Globals;

		uniform highp sampler2D _ClearedMaskTex;

		in vec4 vs_TEXCOORD1;
		in vec2 vs_TEXCOORD0;
		layout(location = 4) in vec4 vs_COLOR0;
		layout(location = 1) out vec4 SV_Target1;
		layout(location = 3) out vec4 SV_Target3;
		layout(location = 0) out vec4 SV_Target0;
		layout(location = 2) out vec4 SV_Target2;
		float _64;
		float _72;
		vec4 _236;

		void main()
		{
		    _64 = _Globals._depth[2] + (-_Globals._depth[3]);
		    _64 = (vs_TEXCOORD0.x * _64) + _Globals._depth[3];
		    _72 = (-_Globals._depth[0]) + _Globals._depth[1];
		    _72 = (vs_TEXCOORD0.x * _72) + _Globals._depth[0];
		    _72 = (-_64) + _72;
		    _64 = (vs_TEXCOORD0.y * _72) + _64;
		    _236 = texture(_ClearedMaskTex, vs_TEXCOORD0);
		    SV_Target0 = vec4(_64) * _Globals._Time + _236 * _Globals._Wet;
		}

		""";

	[Fact]
	public void TheOutputIsMadeWhatTheEditorLinks()
	{
		string g = TerrainShader.Adapt(CrossOutput);
		Assert.DoesNotContain("#version", g);
		Assert.DoesNotContain("_Globals", g);
		Assert.DoesNotContain("struct _32", g);
		Assert.Contains("uniform vec4 _Time;\nuniform vec3 _WorldSpaceCameraPos;\n", g);
		Assert.Contains("uniform float _Wet;", g);
		// The ocean depth: a varying, through the shader's own blend of the zone corners.
		Assert.Contains("const float _depth[4] = float[4](1.0, 1.0, 0.0, 0.0);", g);
		Assert.Contains("in float vs_depth;\nin vec2 vs_maskUV;", g);
		Assert.Contains("texture(_ClearedMaskTex, vs_maskUV)", g);
		Assert.Contains("(vec2(0.0, vs_depth).y * _72)", g);
		Assert.DoesNotContain("vs_TEXCOORD0", g);
		// Fragment inputs link by name; the targets are globals; main is the editor's.
		Assert.Contains("in vec4 vs_COLOR0;", g);
		Assert.DoesNotContain("layout(location", g);
		for (int i = 0; i < 4; i++)
		{
			Assert.Contains($"\nvec4 SV_Target{i};", g);
		}
		Assert.Contains("void valheimGBuffer()", g);
		Assert.DoesNotContain("void main()", g);
		Assert.StartsWith("// Valheim's terrain shader", g);
	}

	[Theory]
	[InlineData("texture(_ClearedMaskTex, vs_TEXCOORD0)", "texture(_ClearedMaskTex, vs_TEXCOORD5)", "paint mask")]
	[InlineData("float _depth[4];", "float _depthB[4];", "ocean depth")]
	[InlineData("uniform _32 _Globals;", "uniform _32 _Other;", "its uniforms")]
	[InlineData("layout(location = 2) out vec4 SV_Target2;", "layout(location = 2) out vec4 Target2;", "G-buffer targets")]
	public void AChangedShaderIsRefusedSayingWhat(string from, string to, string what)
	{
		string changed = CrossOutput.Replace(from, to);
		Assert.NotEqual(CrossOutput, changed);
		var e = Assert.Throws<InvalidDataException>(() => TerrainShader.Adapt(changed));
		Assert.Contains($"The terrain shader changed ({what})", e.Message);
	}

	[Fact]
	public void TheShaderFileIsTurnedIntoGlslOnce()
	{
		string dir = Path.Combine(Path.GetTempPath(), "vwe-shader-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(dir);
		try
		{
			Assert.False(TerrainShader.ConvertIn(dir));
			File.WriteAllBytes(Path.Combine(dir, TerrainShader.SpirvFile), WhiteFragment());
			Assert.Throws<InvalidDataException>(() => TerrainShader.ConvertIn(dir));
			// Nothing half-written is left as the shader.
			Assert.False(File.Exists(Path.Combine(dir, TerrainShader.GlslFile)));
		}
		finally
		{
			Directory.Delete(dir, true);
		}
	}
}

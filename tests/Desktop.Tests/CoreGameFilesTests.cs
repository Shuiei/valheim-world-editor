using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Reading the game's files without the game: textures decoded and bled, Unity's left-handed matrices
// and meshes mirrored into the view's space, materials judged as the old exporter did, the terrain
// shader's OpenGL program adapted, SMOL-V refused when it is not.
public class CoreGameFilesTests
{
	private static GameBundles.TextureLevel Level(int format, int w, int h, params byte[] data) => new("t", format, w, h, data);

	[Fact]
	public void TexturesBecomeRgbaAsTheCopyHadThem()
	{
		// Alpha8: black with that alpha; R8: grey; RGB24: opaque.
		Assert.Equal(new byte[] { 0, 0, 0, 7, 0, 0, 0, 200 }, GameTextures.Rgba(Level(1, 2, 1, 7, 200)));
		Assert.Equal(new byte[] { 9, 9, 9, 255 }, GameTextures.Rgba(Level(63, 1, 1, 9)));
		Assert.Equal(new byte[] { 1, 2, 3, 255 }, GameTextures.Rgba(Level(3, 1, 1, 1, 2, 3)));
		// ARGB32 and BGRA32 reordered.
		Assert.Equal(new byte[] { 1, 2, 3, 4 }, GameTextures.Rgba(Level(5, 1, 1, 4, 1, 2, 3)));
		Assert.Equal(new byte[] { 1, 2, 3, 4 }, GameTextures.Rgba(Level(14, 1, 1, 3, 2, 1, 4)));
		// One DXT1 block of pure red (colour 0 = 0xF800, every pixel index 0): 4 x 4 red, opaque.
		var red = GameTextures.Rgba(Level(10, 4, 4, 0x00, 0xF8, 0, 0, 0, 0, 0, 0))!;
		for (int i = 0; i < 16; i++)
		{
			Assert.Equal(new byte[] { 255, 0, 0, 255 }, red[(i * 4)..(i * 4 + 4)]);
		}
		Assert.Null(GameTextures.Rgba(Level(99, 1, 1, 0)));
		Assert.Equal(8, GameBundles.LevelSize(10, 4, 4));
		Assert.Equal(16 * 4, GameBundles.LevelSize(25, 8, 8));
		Assert.Equal(3 * 5 * 2, GameBundles.LevelSize(3, 5, 2));
	}

	[Fact]
	public void CutOutTexturesTakeTheColourOfTheirOpaquePixels()
	{
		// 4 x 4, one opaque red pixel, the rest transparent black: all red, alpha untouched.
		var px = new byte[4 * 4 * 4];
		px[5 * 4] = 255;
		px[5 * 4 + 3] = 255;
		GameTextures.Bleed(px, 4, 4);
		for (int i = 0; i < 16; i++)
		{
			Assert.Equal(255, px[i * 4]);
			Assert.Equal(0, px[i * 4 + 1]);
			Assert.Equal(i == 5 ? 255 : 0, px[i * 4 + 3]);
		}
		// Nothing opaque: left as it is.
		var none = new byte[] { 10, 20, 30, 0 };
		GameTextures.Bleed(none, 1, 1);
		Assert.Equal(new byte[] { 10, 20, 30, 0 }, none);
	}

	[Fact]
	public void UnitysSpaceIsMirroredIntoTheViews()
	{
		// A move of (1, 2, 3) in Unity is (1, 2, -3) in the view.
		var m = BundleModels.ViewMatrix(GameBundles.Matrix.Trs(1, 2, 3, 0, 0, 0, 1, 1, 1, 1));
		Assert.Equal(new Vector3(1, 2, -3), Vector3.Transform(Vector3.Zero, m));
		// Turned 90° about y (Unity: x goes to -z), then mirrored: the view's x goes to its +z.
		float s = MathF.Sqrt(0.5f);
		var turn = BundleModels.ViewMatrix(GameBundles.Matrix.Trs(0, 0, 0, 0, s, 0, s, 1, 1, 1));
		var x = Vector3.Transform(Vector3.UnitX, turn);
		Assert.Equal(0, x.X, 4);
		Assert.Equal(1, x.Z, 4);
		// Meshes: z mirrored, an up normal when there is none, uv as they are.
		var mesh = BundleModels.ViewMesh(new GameBundles.Mesh("m", 1, new[] { 1f, 2f, 3f }, null, new[] { 0.25f, 0.75f }, new[] { new[] { 0, 0, 0 } }));
		Assert.Equal(new[] { 1f, 2f, -3f, 0f, 1f, 0f, 0.25f, 0.75f }, mesh.Vertices);
		Assert.Equal(new uint[] { 0, 0, 0 }, mesh.Submeshes[0]);
	}

	private static GameBundles.Material Mat(string shader, float? mode = null, float? cull = null, float[]? color = null, bool texture = true)
	{
		var floats = new Dictionary<string, float>();
		if (mode is float m)
		{
			floats["_Mode"] = m;
		}
		if (cull is float c)
		{
			floats["_Cull"] = c;
		}
		var texs = new Dictionary<string, GameBundles.TexEnv>();
		if (texture)
		{
			texs["_MainTex"] = new GameBundles.TexEnv(new GameBundles.Where("b", 5), 2, 3, 0.5f, 0.25f);
		}
		var colors = new Dictionary<string, float[]>();
		if (color != null)
		{
			colors["_Color"] = color;
		}
		return new GameBundles.Material("m", shader, null, texs, floats, colors);
	}

	[Fact]
	public void MaterialsAreJudgedAsTheExporterDid()
	{
		var plain = BundleModels.ViewMaterial(Mat("Standard", color: new[] { 0.5f, 1f, 0f, 0.8f }));
		Assert.Equal(0, plain.Cutoff);
		Assert.False(plain.DoubleSided);
		Assert.Equal("b:5", plain.Map);
		Assert.Equal(new Vector4(2, 3, 0.5f, 0.25f), plain.UvTransform);
		Assert.Equal(MathF.Pow(0.5f, 2.2f), plain.Color.X, 5);
		Assert.Equal(0.8f, plain.Color.W);
		// Vegetation: cut out at half, both sides, its texture bled ("|cut").
		var leaves = BundleModels.ViewMaterial(Mat("Custom/Vegetation"));
		Assert.Equal(0.5f, leaves.Cutoff);
		Assert.True(leaves.DoubleSided);
		Assert.Equal("b:5|cut", leaves.Map);
		// Transparent (Standard's fade mode): drawn cut out at half; no culling: both sides.
		Assert.Equal(0.5f, BundleModels.ViewMaterial(Mat("Standard", mode: 3)).Cutoff);
		Assert.True(BundleModels.ViewMaterial(Mat("Standard", cull: 0)).DoubleSided);
		Assert.Null(BundleModels.ViewMaterial(Mat("Standard", texture: false)).Map);
		Assert.Equal(new GameBundles.Where("bundle:x", -12), GameBundles.Where.FromKey("bundle:x:-12"));
		Assert.Null(GameBundles.Where.FromKey("nothing"));
	}

	private const string Fragment = """
		#ifdef FRAGMENT
		#version 150
		uniform 	vec4 _Time;
		uniform 	float _depth[4];
		UNITY_LOCATION(0) uniform  sampler2DArray _DiffuseArrayTex;
		UNITY_LOCATION(1) uniform  sampler2D _ClearedMaskTex;
		in  vec2 vs_TEXCOORD0;
		layout(location = 0) out vec4 SV_Target0;
		layout(location = 1) out vec4 SV_Target1;
		layout(location = 2) out vec4 SV_Target2;
		layout(location = 3) out vec4 SV_Target3;
		float u_xlat66;
		float u_xlat68;
		void main()
		{
		    u_xlat66 = _depth[2] + (-_depth[3]);
		    u_xlat66 = vs_TEXCOORD0.x * u_xlat66 + _depth[3];
		    u_xlat68 = (-_depth[0]) + _depth[1];
		    u_xlat68 = vs_TEXCOORD0.x * u_xlat68 + _depth[0];
		    u_xlat68 = (-u_xlat66) + u_xlat68;
		    u_xlat66 = vs_TEXCOORD0.y * u_xlat68 + u_xlat66;
		    SV_Target0 = texture(_ClearedMaskTex, vs_TEXCOORD0.xy);
		    SV_Target1 = vec4(u_xlat66);
		    SV_Target2 = vec4(0.0);
		    SV_Target3 = vec4(1.0);
		}
		#endif

		""";

	[Fact]
	public void TheTerrainShadersOpenGlProgramIsAdapted()
	{
		// A variant without the terrain's textures comes first: not the one taken.
		string program = "#ifdef FRAGMENT\nvoid main() { }\n#endif\n" + Fragment.Replace("\r\n", "\n");
		string glsl = GameShader.FromOpenGl(System.Text.Encoding.Latin1.GetBytes(program));
		Assert.StartsWith("// Valheim's terrain shader", glsl);
		Assert.Contains("    u_xlat66 = vs_depth;", glsl);
		Assert.Contains("texture(_ClearedMaskTex, vs_maskUV)", glsl);
		Assert.Contains("in  float vs_depth;\nin  vec2 vs_maskUV;", glsl);
		Assert.Contains("vec4 SV_Target3;", glsl);
		Assert.Contains("uniform  sampler2DArray _DiffuseArrayTex;", glsl);
		Assert.Contains("void valheimGBuffer()", glsl);
		Assert.DoesNotContain("_depth[4]", glsl);
		Assert.DoesNotContain("vs_TEXCOORD0", glsl);
		Assert.DoesNotContain("UNITY_LOCATION", glsl);
		// A game update that changed the shader: said, not drawn broken.
		string changed = program.Replace("    u_xlat66 = vs_TEXCOORD0.y * u_xlat68 + u_xlat66;\n", "", StringComparison.Ordinal);
		var e = Assert.Throws<InvalidDataException>(() => GameShader.FromOpenGl(System.Text.Encoding.Latin1.GetBytes(changed)));
		Assert.Contains("depth block", e.Message);
		Assert.Throws<InvalidDataException>(() => GameShader.FromOpenGl("#ifdef FRAGMENT\nvoid main() { }\n#endif\n"u8.ToArray()));
	}

	[Fact]
	public void OnlySmolvIsDecoded()
	{
		Assert.Equal(0, Smolv.DecodedSize(new byte[30]));
		Assert.Throws<InvalidDataException>(() => Smolv.Decode(new byte[30]));
		Assert.Empty(Smolv.Programs("no programs here"u8.ToArray()));
	}
}

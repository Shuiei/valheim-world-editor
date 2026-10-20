using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading the game's files without the game: textures decoded and bled, the terrain shader's OpenGL program adapted,
/// SMOL-V refused when it is not. (The models made ready for the view: BundleModelsTests, in the app's tests.)
/// </summary>
public class GameBundlesTests
{
    /// <summary>
    /// A texture level of this Unity format, size and bytes.
    /// </summary>
    private static GameBundles.TextureLevel Level(int format, int w, int h, params byte[] data) => new("t", format, w, h, data);

    [Fact]
    public void TexturesBecomeRgbaAsTheCopyHadThem()
    {
        // Alpha8: black with that alpha; R8: grey; RGB24: opaque.
        Assert.Equal(new byte[] { 0, 0, 0, 7, 0, 0, 0, 200 }, GameTextures.Rgba(Level(1, 2, 1, 7, 200)));
        Assert.Equal(new byte[] { 9, 9, 9, 255 }, GameTextures.Rgba(Level(63, 1, 1, "\t"u8.ToArray())));
        Assert.Equal(new byte[] { 1, 2, 3, 255 }, GameTextures.Rgba(Level(3, 1, 1, 1, 2, 3)));
        // ARGB32 and BGRA32 reordered.
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, GameTextures.Rgba(Level(5, 1, 1, 4, 1, 2, 3)));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, GameTextures.Rgba(Level(14, 1, 1, 3, 2, 1, 4)));
        // One DXT1 block of pure red (colour 0 = 0xF800, every pixel index 0): 4 x 4 red, opaque.
        byte[] red = GameTextures.Rgba(Level(10, 4, 4, 0x00, 0xF8, 0, 0, 0, 0, 0, 0))!;
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, red[(i * 4)..(i * 4 + 4)]);
        }
        Assert.Null(GameTextures.Rgba(Level(99, 1, 1, 0)));
        Assert.Equal(8, GameBundles.LevelSize(10, 4, 4));
        Assert.Equal(16 * 4, GameBundles.LevelSize(25, 8, 8));
        Assert.Equal(3 * 5 * 2, GameBundles.LevelSize(3, 5, 2));
    }

    // A range from a broken file is read only when it lies inside the resource file: a skip past the size, or a range
    // running past the end into the next file, is not (it threw, or read the next file's bytes).
    [Theory]
    [InlineData(100, 0, 100, true)]
    [InlineData(100, 40, 60, true)]
    [InlineData(100, 40, 61, false)]
    [InlineData(100, -1, 10, false)]
    [InlineData(100, 10, -5, false)]
    [InlineData(100, long.MaxValue, 10, false)]
    [InlineData(long.MaxValue, 0, 3_000_000_000, false)]
    public void StreamedDataIsReadOnlyInsideItsResourceFile(long length, long offset, long size, bool read)
    {
        Assert.Equal(read, GameBundles.InResource(length, offset, size));
    }

    // A triangle with a corner that is not one of the mesh's vertices is left out (drawing it threw); a part triangle
    // at the end too.
    [Fact]
    public void TrianglesWithCornersOutsideTheMeshAreLeftOut()
    {
        long[] corners = [0, 1, 2, 2, 3, 4, 1, 2, -1, 3, 2, 1, 0];

        Assert.Equal([0, 1, 2, 3, 2, 1], GameBundles.Triangles(corners.Length, 4, k => corners[k]));
    }

    [Fact]
    public void CutOutTexturesTakeTheColourOfTheirOpaquePixels()
    {
        // 4 x 4, one opaque red pixel, the rest transparent black: all red, alpha untouched.
        byte[] px = new byte[4 * 4 * 4];
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
        byte[] none = [10, 20, 30, 0];
        GameTextures.Bleed(none, 1, 1);
        Assert.Equal(new byte[] { 10, 20, 30, 0 }, none);
    }

    /// <summary>
    /// A made-up terrain fragment variant shaped as Unity writes one for OpenGL core: the declarations the editor adapts
    /// (by their names), and a body of its own.
    /// </summary>
    internal const string Fragment = """
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
		float depthHere;
		void main()
		{
		    vec2 uv = vs_TEXCOORD0;
		    depthHere = mix(mix(_depth[0], _depth[1], uv.x), mix(_depth[3], _depth[2], uv.x), uv.y);
		    vec4 mask = texture(_ClearedMaskTex, uv);
		    SV_Target0 = mask;
		    SV_Target1 = vec4(depthHere);
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
        // Its declarations adapted: the corners' depths a global the editor's main sets, the outputs globals.
        Assert.Contains("\nfloat _depth[4];", glsl);
        Assert.DoesNotContain("uniform \tfloat _depth", glsl);
        Assert.Contains("vec4 SV_Target3;", glsl);
        Assert.Contains("uniform  sampler2DArray _DiffuseArrayTex;", glsl);
        Assert.Contains("void valheimGBuffer()", glsl);
        Assert.DoesNotContain("UNITY_LOCATION", glsl);
        // Its statements left as they are, the paint mask's coordinates still its own input.
        Assert.Contains("in  vec2 vs_TEXCOORD0;", glsl);
        Assert.Contains("    vec4 mask = texture(_ClearedMaskTex, uv);\n    SV_Target0 = mask;", glsl);
        // A game update that changed the shader: said, not drawn broken.
        string changed = program.Replace("uniform \tfloat _depth[4];\n", "", StringComparison.Ordinal);
        var e = Assert.Throws<InvalidDataException>(() => GameShader.FromOpenGl(System.Text.Encoding.Latin1.GetBytes(changed)));
        Assert.Contains("ocean depth", e.Message);
        string noMask = program.Replace("in  vec2 vs_TEXCOORD0;\n", "", StringComparison.Ordinal);
        Assert.Contains("texcoords", Assert.Throws<InvalidDataException>(() => GameShader.FromOpenGl(System.Text.Encoding.Latin1.GetBytes(noMask))).Message);
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

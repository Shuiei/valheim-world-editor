using System.Text;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's look besides models (GameLookData), from a made-up game: the terrain's shader (its OpenGL program, or
/// its Vulkan one) and textures, the world map's textures; and what is said when one cannot be read.
/// </summary>
public class GameLookDataTests
{
    /// <summary>
    /// RGBA32 (texture format) and RGBA8 (a texture array's graphics format).
    /// </summary>
    private const int Rgba32 = 4, Rgba8 = 8;

    /// <summary>
    /// What the terrain's game has: which of its parts to leave out or change.
    /// </summary>
    private sealed record Terrain(bool OpenGl = true, bool Vulkan = true, string? MissingTexture = null, bool MissingArray = false, bool Material = true, bool Shader = true);

    /// <summary>
    /// A game with the terrain material (its textures, shader and arrays) and the world map's material, some of its
    /// textures missing.
    /// </summary>
    private static TestGame Game(Terrain t)
    {
        var game = new TestGame();
        var b = game.Bundle("look");
        var blobs = new Dictionary<int, byte[]>();
        if (t.OpenGl)
        {
            blobs[GameShader.OpenGlCore] = Encoding.Latin1.GetBytes(GameBundlesTests.Fragment.Replace("\r\n", "\n"));
        }
        if (t.Vulkan)
        {
            blobs[GameShader.Vulkan] = TestSpirv.Blob(SmolvWriter.Encode(TestSpirv.Program()), TestSpirv.Parameters("Globals", [("_Time", 0), ("_Wet", 16)], [("_MainTex", 1)]));
        }
        if (!t.OpenGl && !t.Vulkan)
        {
            blobs[4] = [1, 2, 3];
        }
        long shader = TestAssets.Shader(b, "Custom/Heightmap", blobs, [new(0, 0, GameShader.SpirvProgram, 1)]);
        // Each texture 1 x 2, its rows two colours.
        var textures = GameLookData.TerrainTextures.Keys.Where(p => p != t.MissingTexture)
            .Select(p => (p, Fields.Ref(TestAssets.Texture(b, p, 1, 2, Rgba32, 1, [1, 2, 3, 4, 5, 6, 7, 8])), (1.0, 1.0), (0.0, 0.0)));
        if (t.Material)
        {
            b.Contents("assets/terrain/Heightmap_basematerial.mat", TestAssets.Material(b, "Heightmap_basematerial", t.Shader ? Fields.Ref(shader) : null, textures));
        }
        b.Contents("assets/terrain/terrain_d_array.texture2darray", TestAssets.TextureArray(b, "terrain_d_array", 1, 1, Rgba8, 3, [.. Enumerable.Range(0, 12).Select(i => (byte)i)]));
        if (!t.MissingArray)
        {
            b.Contents("assets/terrain/terrain_n_array.texture2darray", TestAssets.TextureArray(b, "terrain_n_array", 1, 1, Rgba8, 2, new byte[8]));
        }
        var map = GameLookData.MapTextures.Keys.Take(3).Select(p => (p, Fields.Ref(TestAssets.Texture(b, p, 1, 2, Rgba32, 1, [1, 2, 3, 4, 5, 6, 7, 8])), (1.0, 1.0), (0.0, 0.0)))
            .Append(("_SpaceTex", Fields.Ref(TestAssets.Texture(b, "odd", 1, 1, 99, 1, [0])), (1.0, 1.0), (0.0, 0.0)));
        b.Contents("assets/minimap.mat", TestAssets.Material(b, "minimap", textures: map));
        game.Save();
        return game;
    }

    [Fact]
    public void TheTerrainComesFromItsMaterialShaderAndArrays()
    {
        using var game = Game(new Terrain());
        var t = GameLookData.ReadTerrain(new GameBundles(game.BundlesDir, null));
        Assert.Contains("void valheimGBuffer()", t.Fragment);
        Assert.Equal(GameLookData.TerrainTextures.Values.Order(StringComparer.Ordinal), t.Textures.Keys.Order(StringComparer.Ordinal));
        // As stored: bottom row first.
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], t.Textures["noise"].Rgba);
        // Three slices one below the other.
        Assert.Equal((1, 3), (t.DiffuseArray.Width, t.DiffuseArray.Height));
        Assert.Equal(Enumerable.Range(0, 12).Select(i => (byte)i), t.DiffuseArray.Rgba);
        Assert.Equal(2, t.NormalArray.Height);
    }

    [Fact]
    public void TheVulkanProgramIsTakenWhenAskedOrWhenThereIsNoOpenGlOne()
    {
        // The made-up program is not the terrain shader: SPIRV-Cross reads it, and that it changed is said.
        using var game = Game(new Terrain());
        var e = Assert.Throws<InvalidDataException>(() => GameLookData.ReadTerrain(new GameBundles(game.BundlesDir, null), vulkan: true));
        Assert.Contains("The terrain shader changed", e.Message);
        using var windows = Game(new Terrain(OpenGl: false));
        Assert.Throws<InvalidDataException>(() => GameLookData.ReadTerrain(new GameBundles(windows.BundlesDir, null)));
    }

    [Fact]
    public void ATerrainPartMissingIsSaid()
    {
        static string Fails(Terrain t)
        {
            using var game = Game(t);
            return Assert.Throws<InvalidDataException>(() => GameLookData.ReadTerrain(new GameBundles(game.BundlesDir, null))).Message;
        }
        Assert.Contains("no terrain material", Fails(new Terrain(Material: false)));
        Assert.Contains("_RockNormal", Fails(new Terrain(MissingTexture: "_RockNormal")));
        Assert.Contains("shader could not be read", Fails(new Terrain(Shader: false)));
        Assert.Contains("no OpenGL or Vulkan program", Fails(new Terrain(OpenGl: false, Vulkan: false)));
        Assert.Contains("terrain_n_array", Fails(new Terrain(MissingArray: true)));
    }

    [Fact]
    public void TheMapsTexturesAreThoseThatCanBeRead()
    {
        using var game = Game(new Terrain());
        var map = GameLookData.ReadMap(new GameBundles(game.BundlesDir, null));
        // Three readable (a format not read left out), rows turned top first.
        Assert.Equal(3, map.Count);
        Assert.Equal([5, 6, 7, 8, 1, 2, 3, 4], map["background"].Rgba);
        using var none = new TestGame();
        none.Save();
        Assert.Empty(GameLookData.ReadMap(new GameBundles(none.BundlesDir, null)));
    }
}

using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

using static WorldEditor.Tests.GameBundlesTestAssets;

namespace WorldEditor.Tests;

/// <summary>
/// Materials, textures, crunched textures and texture arrays read from the game's bundles.
/// </summary>
public class GameBundlesTextureTests
{
    [Fact]
    public void MaterialsGiveTheirShaderAndProperties()
    {
        using var game = new TestGame();
        var shaders = game.Bundle("a_shaders");
        long sh = TestAssets.Shader(shaders, "Custom/Piece", new Dictionary<int, byte[]>());
        var b = game.Bundle("b_mats");
        long tex = TestAssets.Texture(b, "wood", 1, 1, Rgba32, 1, [1, 2, 3, 4]);
        long mat = TestAssets.Material(b, "wood_mat", b.RefTo(shaders, sh),
            textures: [("_MainTex", Fields.Ref(tex), (2, 3), (0.5, 0.25)), ("_BumpMap", Fields.Ref(0), (1, 1), (0, 0))],
            floats: [("_Mode", 1), ("_Cutoff", 0.4)],
            colors: [("_Color", 1, 0.5, 0.25, 1)]);
        // A shader that is not one (no parsed form here: a texture): no name.
        long odd = TestAssets.Material(b, "odd", Fields.Ref(tex));
        long none = TestAssets.Material(b, "none");
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var m = bundles.LoadMaterial(new("b_mats", mat))!;
        Assert.Equal("wood_mat", m.Name);
        Assert.Equal("Custom/Piece", m.Shader);
        Assert.Equal(new GameBundles.Where("a_shaders", sh), m.ShaderAt);
        var main = m.Textures["_MainTex"];
        Assert.Equal(new GameBundles.Where("b_mats", tex), main.Texture);
        Assert.Equal((2f, 3f, 0.5f, 0.25f), (main.ScaleX, main.ScaleY, main.OffsetX, main.OffsetY));
        Assert.Null(m.Textures["_BumpMap"].Texture);
        Assert.Equal(0.4f, m.Floats["_Cutoff"]);
        Assert.Equal(new[] { 1f, 0.5f, 0.25f, 1f }, m.Colors["_Color"]);
        Assert.Equal("", bundles.LoadMaterial(new("b_mats", odd))!.Shader);
        var n = bundles.LoadMaterial(new("b_mats", none))!;
        Assert.Null(n.ShaderAt);
        Assert.Equal("", n.Shader);
        Assert.Null(bundles.LoadMaterial(new("b_mats", 777)));
    }

    [Fact]
    public void TexturesGiveTheLargestLevelThatFits()
    {
        using var game = new TestGame();
        var b = game.Bundle("textures");
        // 4 x 2 then 2 x 1 then 1 x 1, each level's pixels one byte: its level number.
        byte[] levels = [.. Enumerable.Repeat((byte)0, 8), .. Enumerable.Repeat((byte)1, 2), 2];
        long kept = TestAssets.Texture(b, "levels", 4, 2, Alpha8, 3, levels);
        long streamed = TestAssets.Texture(b, "streamed", 4, 2, Alpha8, 3, levels, streamed: true);
        long unknown = TestAssets.Texture(b, "unknown", 4, 4, 99, 1, new byte[16]);
        long empty = TestAssets.Texture(b, "empty", 0, 4, Alpha8, 1, []);
        long tooShort = TestAssets.Texture(b, "short", 4, 4, Rgba32, 1, new byte[10]);
        long mat = TestAssets.Material(b, "not a texture");
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var top = bundles.LoadTexture(new("textures", kept), 1024)!;
        Assert.Equal(("levels", Alpha8, 4, 2), (top.Name, top.Format, top.Width, top.Height));
        Assert.All(top.Data, x => Assert.Equal(0, x));
        var mid = bundles.LoadTexture(new("textures", kept), 2)!;
        Assert.Equal((2, 1), (mid.Width, mid.Height));
        Assert.All(mid.Data, x => Assert.Equal(1, x));
        // None small enough: the smallest there is.
        var least = bundles.LoadTexture(new("textures", streamed), 0)!;
        Assert.Equal((1, 1), (least.Width, least.Height));
        Assert.Equal([2], least.Data);
        Assert.Equal(1, bundles.LoadTexture(new("textures", streamed), 2)!.Data[0]);
        Assert.Null(bundles.LoadTexture(new("textures", unknown), 1024));
        Assert.Null(bundles.LoadTexture(new("textures", empty), 1024));
        Assert.Null(bundles.LoadTexture(new("textures", tooShort), 1024));
        Assert.Null(bundles.LoadTexture(new("textures", mat), 1024));
        Assert.Null(bundles.LoadTexture(new("textures", 4242), 1024));
    }

    [Fact]
    public void CrunchedTexturesGiveTheirLevelAsDxtBlocks()
    {
        using var game = new TestGame();
        var b = game.Bundle("crunched");
        // The crn files of CrunchTests: DXT1 64 x 64 in 7 levels, DXT5 48 x 20 in 6; the Texture2D's own size and mip
        // count are not used (the crn file has its own).
        byte[] dxt1 = File.ReadAllBytes(Path.Combine(Fixtures.Root, "crunch-dxt1.crn"));
        byte[] dxt5 = File.ReadAllBytes(Path.Combine(Fixtures.Root, "crunch-dxt5.crn"));
        long kept = TestAssets.Texture(b, "kept", 48, 20, 29, 1, dxt5);
        long streamed = TestAssets.Texture(b, "streamed", 64, 64, 28, 1, dxt1, streamed: true);
        long broken = TestAssets.Texture(b, "broken", 64, 64, 28, 7, dxt1[..500]);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var top = bundles.LoadTexture(new("crunched", kept), 1024)!;
        Assert.Equal(("kept", 12, 48, 20), (top.Name, top.Format, top.Width, top.Height));
        Assert.Equal(Crunch.Unpack(dxt5, 0), top.Data);
        var mid = bundles.LoadTexture(new("crunched", kept), 24)!;
        Assert.Equal((24, 10), (mid.Width, mid.Height));
        Assert.Equal(Crunch.Unpack(dxt5, 1), mid.Data);
        var least = bundles.LoadTexture(new("crunched", streamed), 0)!;
        Assert.Equal((10, 1, 1), (least.Format, least.Width, least.Height));
        Assert.Equal(Crunch.Unpack(dxt1, 6), least.Data);
        Assert.Equal(GameBundles.LevelSize(10, 32, 32), bundles.LoadTexture(new("crunched", streamed), 32)!.Data.Length);
        Assert.Null(bundles.LoadTexture(new("crunched", broken), 1024));
    }

    [Fact]
    public void CrunchedTexturesAreReadFromUnity2017Point3On()
    {
        Assert.False(GameBundles.UnityCrunchFormat("5.6.7f1"));
        Assert.False(GameBundles.UnityCrunchFormat("2017.2.5f1"));
        Assert.True(GameBundles.UnityCrunchFormat("2017.3.0f3"));
        Assert.True(GameBundles.UnityCrunchFormat("2022.3.62f1"));
        Assert.True(GameBundles.UnityCrunchFormat("6000.0.74f1"));
        // Not given (stripped bundles): taken as new.
        Assert.True(GameBundles.UnityCrunchFormat("0.0.0"));
        Assert.True(GameBundles.UnityCrunchFormat(""));
    }

    [Fact]
    public void TextureArraysGiveEachSlice()
    {
        using var game = new TestGame();
        var b = game.Bundle("arrays");
        // Two 1 x 1 RGBA slices (graphics format 8: RGBA8 sRGB, read as RGBA32).
        long kept = TestAssets.TextureArray(b, "pair", 1, 1, 8, 2, [1, 2, 3, 4, 5, 6, 7, 8]);
        long streamed = TestAssets.TextureArray(b, "pair_streamed", 1, 1, 4, 2, [1, 2, 3, 4, 5, 6, 7, 8], streamed: true);
        // DXT1 (graphics format 96): one 4 x 4 block of 8 bytes per slice.
        long dxt = TestAssets.TextureArray(b, "dxt", 4, 4, 96, 1, new byte[8]);
        long unknown = TestAssets.TextureArray(b, "unknown", 1, 1, 200, 1, [1, 2, 3, 4]);
        // Graphics format 5 (R8) is not TextureFormat 5 (ARGB32): not read (it was, as ARGB32).
        long r8 = TestAssets.TextureArray(b, "r8", 1, 1, 5, 1, [1, 2, 3, 4]);
        long missing = TestAssets.TextureArray(b, "missing", 2, 2, 8, 2, [1, 2, 3, 4]);
        long tex = TestAssets.Texture(b, "plain", 1, 1, Rgba32, 1, [1, 2, 3, 4]);
        var bundles = SavedGameBundles.Of(game);

        var slices = bundles.LoadTextureArray(new("arrays", kept))!;
        Assert.Equal(["pair[0]", "pair[1]"], slices.Select(s => s.Name));
        Assert.Equal([5, 6, 7, 8], slices[1].Data);
        Assert.Equal(Rgba32, slices[0].Format);
        Assert.Equal([1, 2, 3, 4], bundles.LoadTextureArray(new("arrays", streamed))![0].Data);
        Assert.Equal(10, bundles.LoadTextureArray(new("arrays", dxt))![0].Format);
        Assert.Null(bundles.LoadTextureArray(new("arrays", unknown)));
        Assert.Null(bundles.LoadTextureArray(new("arrays", r8)));
        Assert.Null(bundles.LoadTextureArray(new("arrays", missing)));
        Assert.Null(bundles.LoadTextureArray(new("arrays", tex)));
    }
}

using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

using static WorldEditor.Tests.GameBundlesTestAssets;

namespace WorldEditor.Tests;

/// <summary>
/// Shaders read from the game's bundles: each platform's program and the deferred pass, and the shader blobs that do not
/// unpack.
/// </summary>
public class GameBundlesShaderTests
{
    [Fact]
    public void ShadersGiveEachPlatformsProgramAndTheDeferredPass()
    {
        using var game = new TestGame();
        var b = game.Bundle("shaders");
        byte[] gl = [.. Enumerable.Range(0, 300).Select(i => (byte)i)];
        byte[] vk = "vulkan program"u8.ToArray();
        long sh = TestAssets.Shader(b, "Custom/Heightmap", new Dictionary<int, byte[]> { [15] = gl, [18] = vk },
            [new(0, 0, 25, 1), new(2, 1, 25, 3)], part: 128);
        long other = TestAssets.Shader(b, "Custom/Other", new Dictionary<int, byte[]> { [15] = gl }, [new(0, 0, 25, 1)], pass: "FORWARD_DELTA");
        long tex = TestAssets.Texture(b, "plain", 1, 1, Rgba32, 1, [1, 2, 3, 4]);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var p = bundles.LoadShader(new("shaders", sh))!;
        Assert.Equal([15, 18], p.Platforms);
        Assert.Equal(gl, p.Blobs[15]);
        Assert.Equal(vk, p.Blobs[18]);
        Assert.Equal([new GameShader.SubProgram(25, 0, 0, 1), new GameShader.SubProgram(25, 1, 2, 3)], p.Deferred);
        Assert.Empty(bundles.LoadShader(new("shaders", other))!.Deferred);
        Assert.Null(bundles.LoadShader(new("shaders", tex)));
    }

    [Fact]
    public void AShaderBlobThatDoesNotUnpackIsSaid()
    {
        using var game = new TestGame();
        var b = game.Bundle("shaders");
        byte[] packed = UnityFsWriter.Literals(new byte[64]);
        // A part said to unpack to more bytes than it does, and one that is not LZ4 at all.
        long shorter = BrokenShader(b, packed, 100);
        long garbage = BrokenShader(b, [0xFF, 0xFF, 1, 2], 100);
        long whole = BrokenShader(b, packed, 64);
        // Lengths that do not fit together (two offsets, one length; a platform with no parts listed), a part past the
        // data's end, and one said to unpack to 4 GB: said too (they threw other errors, or made room for 4 GB).
        long mismatched = BrokenShader(b, packed, 64, [new List<object> { 0u, 0u }]);
        long unlisted = BrokenShader(b, packed, 64, []);
        long past = BrokenShader(b, packed, 64, [new List<object> { 10u }]);
        long huge = BrokenShader(b, packed, uint.MaxValue);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        Assert.Throws<InvalidDataException>(() => bundles.LoadShader(new("shaders", shorter)));
        Assert.Throws<InvalidDataException>(() => bundles.LoadShader(new("shaders", garbage)));
        Assert.Equal(new byte[64], bundles.LoadShader(new("shaders", whole))!.Blobs[15]);
        Assert.Throws<InvalidDataException>(() => bundles.LoadShader(new("shaders", mismatched)));
        Assert.Throws<InvalidDataException>(() => bundles.LoadShader(new("shaders", unlisted)));
        Assert.Throws<InvalidDataException>(() => bundles.LoadShader(new("shaders", past)));
        Assert.Throws<InvalidDataException>(() => bundles.LoadShader(new("shaders", huge)));
    }

    /// <summary>
    /// A shader with one OpenGL program of one part: these packed bytes, said to unpack to <paramref name="size"/>, at
    /// these offsets (each platform's list; default one part at 0).
    /// </summary>
    private static long BrokenShader(TestBundle b, byte[] packed, uint size, List<object>? offsets = null) =>
        b.Assets.Add(UnityClass.Shader, UnityTypes.Shader, new Fields
        {
            ["platforms"] = new List<object> { 15u },
            ["offsets"] = offsets ?? [new List<object> { 0u }],
            ["compressedLengths"] = new List<object> { new List<object> { (uint)packed.Length } },
            ["decompressedLengths"] = new List<object> { new List<object> { size } },
            ["compressedBlob"] = packed,
        });
}

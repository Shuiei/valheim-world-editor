using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The terrain shader's Vulkan program (GameShader.FromVulkan), on a made-up platform blob (TestSpirv): the deferred
/// pass's fragment program without keywords or tessellation found among its sub-programs, its uniform block, members,
/// texture, colour input and outputs named from its parameter blob; and what is said when they cannot be.
/// </summary>
public class GameShaderVulkanTests
{
    /// <summary>
    /// A program of a stage as SMOL-V.
    /// </summary>
    private static byte[] Smol(uint stage, uint binding = 1) => SmolvWriter.Encode(TestSpirv.Program(stage, binding));

    /// <summary>
    /// The usual parameter blob: Globals with _Time at 0 and _Wet at 16, _MainTex at binding 1.
    /// </summary>
    private static readonly byte[] Params = TestSpirv.Parameters("Globals", [("_Time", 0), ("_Wet", 16)], [("_MainTex", 1)]);

    /// <summary>
    /// A deferred pass's sub-program.
    /// </summary>
    private static GameShader.SubProgram Sub(int blob, int parameters, int keywords = 0, int type = GameShader.SpirvProgram) => new(type, keywords, blob, parameters);

    /// <summary>
    /// SPIR-V bytes as words.
    /// </summary>
    private static uint[] Words(byte[] b)
    {
        uint[] w = new uint[b.Length / 4];
        Buffer.BlockCopy(b, 0, w, 0, b.Length);
        return w;
    }

    [Fact]
    public void TheDeferredFragmentProgramIsFoundAndNamed()
    {
        byte[] blob = TestSpirv.Blob(
            [.. Smol(TestSpirv.Vertex), .. Smol(TestSpirv.Fragment)],
            Params,
            [.. Smol(TestSpirv.Fragment), .. Smol(TestSpirv.TessControl)],
            Smol(TestSpirv.Vertex));
        // Passed over: not SPIR-V, a variant with keywords, one with tessellation, one without a fragment program,
        // entries the blob does not have.
        var subs = new[] { Sub(0, 1, type: 5), Sub(0, 1, keywords: 2), Sub(2, 1), Sub(3, 1), Sub(9, 1), Sub(0, 9), Sub(0, 1) };
        var names = TestSpirv.Names(Words(GameShader.FromVulkan(blob, subs)));
        Assert.Equal("_Globals", names[$"{TestSpirv.Globals}"]);
        Assert.Equal("_Time", names[$"{TestSpirv.Block}.0"]);
        Assert.Equal("_Wet", names[$"{TestSpirv.Block}.1"]);
        Assert.Equal("_MainTex", names[$"{TestSpirv.Texture}"]);
        Assert.Equal("vs_COLOR0", names[$"{TestSpirv.Color}"]);
        Assert.Equal("SV_Target1", names[$"{TestSpirv.Target}"]);
        // Names it had are kept.
        Assert.Equal("vs_TEXCOORD0", names[$"{TestSpirv.Uv}"]);
        Assert.Equal("main", names[$"{TestSpirv.Main}"]);
    }

    [Fact]
    public void WhatCannotBeNamedIsSaid()
    {
        static byte[] Blob(byte[] program, byte[] parameters) => TestSpirv.Blob(program, parameters);
        GameShader.SubProgram[] subs = [Sub(0, 1)];
        // No usable sub-program.
        Assert.Throws<InvalidDataException>(() => GameShader.FromVulkan(Blob(Smol(TestSpirv.Vertex), Params), subs));
        // A uniform block whose offsets no buffer of the parameters has (and a buffer whose name is not Globals).
        byte[] other = TestSpirv.Parameters("Globals", [("_Time", 0), ("_Wet", 20)], [("_MainTex", 1)]);
        Assert.Throws<InvalidDataException>(() => GameShader.FromVulkan(Blob(Smol(TestSpirv.Fragment), other), subs));
        byte[] notGlobals = TestSpirv.Parameters("Locals", [("_Time", 0), ("_Wet", 16)], [("_MainTex", 1)]);
        Assert.Throws<InvalidDataException>(() => GameShader.FromVulkan(Blob(Smol(TestSpirv.Fragment), notGlobals), subs));
        // A texture binding no texture of the parameters has.
        Assert.Contains("binding 5", Assert.Throws<InvalidDataException>(() => GameShader.FromVulkan(Blob(Smol(TestSpirv.Fragment, binding: 5), Params), subs)).Message);
        // An instruction of no length (SMOL-V cannot hold one: the SPIR-V itself).
        uint[] broken = TestSpirv.Program();
        broken[5] = 0;
        Assert.Throws<InvalidDataException>(() => GameShader.Named(broken, GameShader.Parameters(Params)));
        // One running past the end, and no whole header (other exceptions came out).
        uint[] past = TestSpirv.Program();
        past[5] = (uint)(past.Length << 16) | (past[5] & 0xFFFF);
        Assert.Throws<InvalidDataException>(() => GameShader.Named(past, GameShader.Parameters(Params)));
        Assert.Throws<InvalidDataException>(() => GameShader.Named([0x07230203, 0], GameShader.Parameters(Params)));
        // A blob listing more entries than it holds, or an entry outside it.
        Assert.Throws<InvalidDataException>(() => GameShader.FromVulkan([5, 0, 0, 0], subs));
        Assert.Throws<InvalidDataException>(() => GameShader.FromVulkan([1, 0, 0, 0, 100, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0], subs));
    }

    [Fact]
    public void TheNamedProgramGoesThroughSpirvCross()
    {
        // SPIRV-Cross reads it; it is not the terrain shader, which is said.
        byte[] named = GameShader.FromVulkan(TestSpirv.Blob(Smol(TestSpirv.Fragment), Params), [Sub(0, 1)]);
        Assert.Contains("The terrain shader changed", Assert.Throws<InvalidDataException>(() => TerrainShader.FromSpirv(named)).Message);
    }
}

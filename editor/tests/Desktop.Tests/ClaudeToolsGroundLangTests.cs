using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's ground tools while the window shows another language (the made-up one: PseudoLang): biomes are still
/// found by their English names, and what Claude is told about a wrong biome, settings that let nothing through, or
/// a path or cave dug along a line stays English.
/// </summary>
[Collection("DataDir")]
public sealed class ClaudeToolsGroundLangTests
{
    [Fact]
    public void ClaudesMaskStaysEnglish()
    {
        using var pseudo = new PseudoLang();
        var s = TestScenes.Flat(1);
        s.Scene.Biomes[100] = 8;
        Assert.Equal(1, ClaudeTools.MaskOf(new ClaudeTools.MaskSpec { Biomes = ["Black Forest"] }, s).Mask!(100));
        Assert.StartsWith("Lava: the biomes are Meadows, Black Forest", ClaudeTools.MaskOf(new ClaudeTools.MaskSpec { Biomes = ["Lava"] }, s).Problem);
        Assert.Equal(
            "The mask lets nothing through: Slope max 0° only lets perfectly flat ground through: empty the box for no limit.",
            ClaudeTools.MaskOf(new ClaudeTools.MaskSpec { SlopeMax = 0 }, s).Problem);
    }

    [AvaloniaFact]
    public async Task ClaudesPathsAndCavesStayEnglish()
    {
        using var pseudo = new PseudoLang();
        using var r = new ClaudeRun();
        await r.OpenArea();
        string flat = await r.T.DigPath("flatten", [[-20f, 0f], [20f, 0f]]);
        Assert.StartsWith("Flatten to height along ", flat);
        Assert.DoesNotContain(PseudoLang.Open, flat);
        string cave = await r.T.DigPath("cave", [[-20f, 15f], [20f, 15f]], depth: 8, headroom: 4);
        Assert.StartsWith("Dug ", cave);
        Assert.DoesNotContain(PseudoLang.Open, cave);
    }
}

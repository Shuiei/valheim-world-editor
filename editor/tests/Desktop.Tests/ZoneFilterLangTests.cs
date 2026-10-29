using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The biomes' names in another language (the made-up one: PseudoLang): the biomes offered and a biome's name come in
/// the language in use, and in English when asked for (what Claude is told and names biomes by); the map's biome filter
/// buttons show them in the language.
/// </summary>
[Collection("DataDir")]
public sealed class ZoneFilterLangTests
{
    [AvaloniaFact]
    public void TheBiomesAreNamedInTheLanguageAndInEnglishWhenAsked()
    {
        var english = ZoneFilter.BiomeChoices(english: true);
        using var pseudo = new PseudoLang();
        var shown = ZoneFilter.BiomeChoices();
        Assert.Equal(english.Select(b => (b.Value, PseudoLang.Wrap(b.Name))), shown);
        Assert.Equal(english, ZoneFilter.BiomeChoices(english: true));

        var meadows = ZoneFilter.BiomeName(ValheimGen.Heightmap.Biome.Meadows);
        Assert.Equal(PseudoLang.Wrap("Meadows"), meadows.Shown);
        Assert.Equal("Meadows", meadows.English);
        // A biome not offered keeps its type's own name, the same in both.
        var none = ZoneFilter.BiomeName(ValheimGen.Heightmap.Biome.None);
        Assert.Equal("None", none.Shown);
        Assert.Equal("None", none.English);
    }

    [AvaloniaFact]
    public void TheMapsBiomeButtonsShowTheBiomesInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        var map = new MapPage();
        Assert.Equal(
            ZoneFilter.BiomeChoices(english: true).Select(b => (b.Value, PseudoLang.Wrap(b.Name))),
            map.BiomeButtons.Select(b => (b.Key, (string)b.Value.Content!)));
    }
}

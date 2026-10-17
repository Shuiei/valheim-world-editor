using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Mountain panel in another language (the made-up one: PseudoLang): with each preset chosen (its help line, the
/// Turn row of ridges and ranges) it shows only catalogue texts, while the presets keep the English names scripts give.
/// </summary>
[Collection("DataDir")]
public sealed class MountainPanelLangTests
{
    [AvaloniaFact]
    public void MountainPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var p = new MountainPanel();
        for (int i = 0; i < Mountain.Presets.Length; i++)
        {
            p.PresetBox.SelectedIndex = i;
            Assert.Empty(PseudoLang.Untranslated(p.Card));
            Assert.StartsWith(PseudoLang.Open.ToString(), p.HelpText.Text, StringComparison.Ordinal);
        }
        // Scripts name a preset in English, whatever language the window shows.
        Assert.Equal("Lone peak", Mountain.Presets[0].Name);
        Assert.Equal(PseudoLang.Wrap("Lone peak"), Mountain.Presets[0].Shown);
    }
}

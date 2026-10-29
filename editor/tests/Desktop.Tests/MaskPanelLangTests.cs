using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Mask panel in another language (the made-up one: PseudoLang): on, with its biomes, ranges, paint rules and a
/// warning about settings that let nothing through, it shows only catalogue texts.
/// </summary>
[Collection("DataDir")]
public sealed class MaskPanelLangTests
{
    [AvaloniaFact]
    public void MaskPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var mask = new Mask { On = true, SlopeMin = 40, SlopeMax = 0, HeightMin = 50, HeightMax = 20 };
        var panel = new MaskPanel(mask);
        Assert.True(panel.WarningText.IsVisible);
        Assert.StartsWith(PseudoLang.Open.ToString(), panel.WarningText.Text);
        Assert.Empty(PseudoLang.Untranslated(panel.Card));
    }
}

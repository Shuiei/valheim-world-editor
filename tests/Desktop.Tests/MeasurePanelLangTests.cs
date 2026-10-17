using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Measure panel in another language (the made-up one: PseudoLang): before the first click and with a tape fixed
/// (its labels and measured values), it shows only catalogue texts.
/// </summary>
[Collection("DataDir")]
public sealed class MeasurePanelLangTests
{
    [AvaloniaFact]
    public void MeasurePanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var w = TestWindows.Editing(TestScenes.Flat(2));
        Assert.Empty(PseudoLang.Untranslated(w.MeasurePanel.Card));

        w.View.Tape.Down(new Vector3(-10, 25, 0));
        w.View.Tape.Down(new Vector3(10, 30, 0));
        Assert.Equal(6, w.MeasurePanel.Shown.Count);
        Assert.Empty(PseudoLang.Untranslated(w.MeasurePanel.Card));
    }
}

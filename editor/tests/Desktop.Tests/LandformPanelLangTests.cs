using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Landform panel in another language (the made-up one: PseudoLang): with each kind chosen it shows only catalogue
/// texts, and a picture's step and messages are in that language while Claude reads the messages in English.
/// </summary>
[Collection("DataDir")]
public sealed class LandformPanelLangTests
{
    [AvaloniaFact]
    public void LandformPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var p = new LandformPanel();
        foreach (var kind in Enum.GetValues<LandformKind>())
        {
            p.KindBox.SelectedIndex = (int)kind;
            Assert.Empty(PseudoLang.Untranslated(p.Card));
            Assert.StartsWith(PseudoLang.Open.ToString(), p.HelpText.Text, StringComparison.Ordinal);
            Assert.StartsWith(PseudoLang.Open.ToString(), p.LimitText.Text, StringComparison.Ordinal);
        }
        p.ShowNoLimit(true);
        Assert.Empty(PseudoLang.Untranslated(p.Card));
        Assert.Equal(PseudoLang.Wrap("Landform: {0}").Replace("{0}", PseudoLang.Wrap("Rough hill"), StringComparison.Ordinal), p.Picture.Label);
    }

    [AvaloniaFact]
    public void PuttingAPictureIsSaidInTheLanguageAndClaudeReadsEnglish()
    {
        var s = TestScenes.Flat(2);
        var w = TestWindows.Editing(s);
        using var pseudo = new PseudoLang();
        w.PutPicture(64, 64);
        Assert.StartsWith(PseudoLang.Open.ToString(), w.MessageText.Text, StringComparison.Ordinal);
        Assert.Matches(@"^Placed the picture, [\d.]+ m up\. Ctrl\+Z undoes it\.$", w.LastMessageEn);
        Assert.StartsWith(PseudoLang.Open.ToString(), s.UndoLabel, StringComparison.Ordinal);
        w.LandformPanel.Picture.HeightBox.Value = 0;
        w.PutPicture(64, 64);
        Assert.Equal("Nothing to put in: pick a picture and a Height other than 0.", w.LastMessageEn);
    }
}

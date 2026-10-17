using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Shape tool in another language (the made-up one: PseudoLang): its panel shows only catalogue texts, with each
/// preset, a formula of one's own and a formula's mistake; putting a shape says so in that language and keeps the
/// English for Claude, and its History step is named in that language.
/// </summary>
[Collection("DataDir")]
public sealed class ShapePanelLangTests
{
    [AvaloniaFact]
    public void ShapePanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var p = new ShapePanel();
        Assert.Empty(PseudoLang.Untranslated(p.Card));
        Assert.Equal(PseudoLang.Wrap("Shape: {0}").Replace("{0}", PseudoLang.Wrap("Mound"), StringComparison.Ordinal), p.Label);

        // A formula of one's own.
        p.FormulaBox.Text = "h * 2";
        Assert.Equal(ShapePanel.Presets.Length, p.PresetBox.SelectedIndex);
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        // A formula's mistake, in the panel and in the message the formula gave.
        p.FormulaBox.Text = "h * ";
        Assert.Equal(PseudoLang.Wrap("Formula: {0}.").Replace("{0}", PseudoLang.Wrap("the formula ends too early"), StringComparison.Ordinal), p.ErrorText.Text);
        Assert.Empty(PseudoLang.Untranslated(p.Card));
        p.FormulaBox.Text = "sin(1, 2) + x";
        Assert.Empty(PseudoLang.Untranslated(p.Card));
    }

    [AvaloniaFact]
    public void PuttingAShapeIsSaidInTheLanguageAndClaudeReadsEnglish()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        using var pseudo = new PseudoLang();

        w.PutShape(64, 64);
        Assert.StartsWith(PseudoLang.Open.ToString(), w.MessageText.Text, StringComparison.Ordinal);
        Assert.Matches(@"^Placed the shape on \d+ point\(s\)\. Ctrl\+Z undoes it\.$", w.LastMessageEn);
        Assert.StartsWith(PseudoLang.Open.ToString(), s.UndoLabel, StringComparison.Ordinal);
    }
}

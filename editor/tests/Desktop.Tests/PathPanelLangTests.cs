using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path panel in another language (the made-up one: PseudoLang): its actions, rows and the line's
/// length show only catalogue texts; Apply without a line says so in the
/// language, its English kept for Claude.
/// </summary>
[Collection("DataDir")]
public sealed class PathPanelLangTests
{
    [AvaloniaFact]
    public void PathPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var w = TestWindows.Editing(TestScenes.Flat(2));
        var p = w.PathPanel;
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        w.ApplyPath();
        Assert.Equal(PseudoLang.Wrap("Draw a line with at least two points first."), w.MessageText.Text);
        Assert.Equal("Draw a line with at least two points first.", w.LastMessageEn);

        // A line drawn, with the Natural look's rows: every row and the line's length.
        w.View.Path.Points.Add(new Vector2(20, 64));
        w.View.Path.Points.Add(new Vector2(108, 64));
        w.View.Path.Notify();
        p.NaturalBox.IsChecked = true;
        Assert.Empty(PseudoLang.Untranslated(p.Card));
    }
}

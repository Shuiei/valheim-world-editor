using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path panel in another language (the made-up one: PseudoLang): its actions, rows, cave presets and roofs, a
/// randomized preset's note and the line's length show only catalogue texts; Apply without a line says so in the
/// language, its English kept for Claude.
/// </summary>
[Collection("DataDir")]
public sealed class PathPanelLangTests
{
    [AvaloniaFact]
    public void PathPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var (w, _) = TestWindows.Editing();
        var p = w.PathPanel;
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        w.ApplyPath();
        Assert.Equal(PseudoLang.Wrap("Draw a line with at least two points first."), w.MessageText.Text);
        Assert.Equal("Draw a line with at least two points first.", w.LastMessageEn);

        // The cave, randomized, and a line drawn: every row, the preset's note and the line's length.
        p.ChooseCave();
        p.CaveRandomButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        w.View.Path.Points.Add(new Vector2(20, 64));
        w.View.Path.Points.Add(new Vector2(108, 64));
        w.View.Path.Notify();
        p.NaturalBox.IsChecked = true;
        Assert.Empty(PseudoLang.Untranslated(p.Card));
    }
}

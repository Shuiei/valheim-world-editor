using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The brush options in another language (the made-up one: PseudoLang): every brush's rows, the shapes, stamps and
/// falloffs show only catalogue texts; a brush's message is shown in the language in use with its English kept, the
/// cursor readout is a catalogue text, and Claude's stamp tool still knows the stamps by their English names and answers
/// in English.
/// </summary>
[Collection("DataDir")]
public sealed class ToolPanelLangTests
{
    [AvaloniaFact]
    public async Task ToolPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var t = new ToolPanel();
        foreach (var tool in new[] { BrushTool.Raise, BrushTool.Flatten, BrushTool.Natural, BrushTool.Erode, BrushTool.PaintDirt })
        {
            t.Choose(tool);
            Assert.Empty(PseudoLang.Untranslated(t.Options));
        }
        // A built-in stamp as the shape: its own rows (Turn, Stamp once).
        t.Choose(BrushTool.Raise);
        t.ShapeBox.SelectedIndex = 4;
        t.StampOnceBox.IsChecked = true;
        Assert.Equal(PseudoLang.Wrap("Stamp: Mountain"), t.ShapeBox.SelectedItem);
        Assert.Empty(PseudoLang.Untranslated(t.Options));

        Said? said = null;
        t.Message += m => said = m;
        t.NewPattern(7);
        Assert.Equal(PseudoLang.Wrap("New natural pattern. The next Naturalize stroke or natural path uses it."), said!.Shown);
        Assert.Equal("New natural pattern. The next Naturalize stroke or natural path uses it.", said.English);

        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        Assert.True(PseudoLang.Translated(CursorReadout.Text(s.Scene, 10, 10), []));
        var claude = new ClaudeTools(w);
        string none = await claude.Stamp("nothing", 0, 0);
        Assert.DoesNotContain(PseudoLang.Open, none);
        Assert.Contains("Mesa (flat top)", none);
        string done = await claude.Stamp("mesa", 0, 0, radius: 10);
        Assert.StartsWith("Stamped ", done);
        Assert.DoesNotContain(PseudoLang.Open, done);
    }
}

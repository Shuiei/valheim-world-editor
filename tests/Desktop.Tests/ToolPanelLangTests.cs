using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The brush options in another language (the made-up one: PseudoLang): every brush's rows, the shapes, stamps and
/// falloffs show only catalogue texts, and the Workshop's rail its Build; a brush's message is shown in the language in
/// use with its English kept, the cursor readout (unedited, edited, at the limit) is a catalogue text, and Claude's stamp
/// tool still knows the stamps by their English names and answers in English. A built-in stamp chosen as the shape comes
/// back in another language; a loaded picture's entry carries its name, so its English cannot be found from it.
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

        t.SetWorkshop(true);
        Assert.Contains(PseudoLang.Wrap("Build"), t.Rail.GetLogicalDescendants().OfType<TextBlock>().Select(b => b.Text));
        t.SetWorkshop(false);

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
        // Unedited, edited, then at the game's limit.
        string plain = CursorReadout.Text(s.Scene, 10, 10);
        Assert.True(PseudoLang.Translated(plain, []), plain);
        s.Shape(10, 10, Formula.Compile("2", []), 3, 0, "raise");
        string edited = CursorReadout.Text(s.Scene, 10, 10);
        Assert.Contains(PseudoLang.Wrap("ground 32.00 m (original 30.00, +2.00)"), edited);
        Assert.True(PseudoLang.Translated(edited, []), edited);
        s.Shape(10, 10, Formula.Compile("20", []), 3, 0, "raise more");
        string limit = CursorReadout.Text(s.Scene, 10, 10);
        Assert.Contains(PseudoLang.Wrap("ground 38.00 m (original 30.00, +8.00, at the ±8 m limit)"), limit);
        Assert.True(PseudoLang.Translated(limit, []), limit);
        var claude = new ClaudeTools(w);
        string none = await claude.Stamp("nothing", 0, 0);
        Assert.DoesNotContain(PseudoLang.Open, none);
        Assert.Contains("Mesa (flat top)", none);
        string done = await claude.Stamp("mesa", 0, 0, radius: 10);
        Assert.StartsWith("Stamped ", done);
        Assert.DoesNotContain(PseudoLang.Open, done);
    }

    [AvaloniaFact]
    public void ABuiltInStampChosenComesBackInAnotherLanguage()
    {
        var p = Prefs.InMemory();
        var english = new ToolPanel();
        p.Bind(english.ShapeBox, "brush.shape");
        english.ShapeBox.SelectedIndex = 5;
        Assert.Equal("Stamp: Mesa (flat top)", p.Get("brush.shape", ""));
        using (new PseudoLang())
        {
            var pseudo = new ToolPanel();
            p.Bind(pseudo.ShapeBox, "brush.shape");
            Assert.Equal(5, pseudo.ShapeBox.SelectedIndex);
            pseudo.ShapeBox.SelectedIndex = 6;
        }
        var back = new ToolPanel();
        p.Bind(back.ShapeBox, "brush.shape");
        Assert.Equal(6, back.ShapeBox.SelectedIndex);
        Assert.Equal("Stamp: Crater rim", back.ShapeBox.SelectedItem);
    }

    [AvaloniaFact]
    public void ALoadedStampIsNamedInTheLanguageInUse()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-stamps-" + Guid.NewGuid().ToString("N")[..8]);
        string? was = Stamps.PathOverride;
        try
        {
            Stamps.PathOverride = Path.Combine(dir, "stamps.json");
            using var pseudo = new PseudoLang();
            var t = new ToolPanel();
            Said? said = null;
            t.Message += m => said = m;
            t.AddStamp("pic", new float[Stamps.Size * Stamps.Size]);
            Assert.Equal(PseudoLang.Wrap("Stamp: pic"), t.ShapeBox.SelectedItem);
            Assert.Equal("Loaded the stamp “pic”: white parts work fully, black parts not at all.", said!.English);
            Assert.True(PseudoLang.Translated(said.Shown, []), said.Shown);
            Assert.Equal("pic", t.Brush.StampLabel);
            // Its entry holds its name as a value: no catalogue text gives its English back (the known limit of a
            // remembered loaded stamp across languages).
            Assert.Equal(PseudoLang.Wrap("Stamp: pic"), Lang.EnglishOf(PseudoLang.Wrap("Stamp: pic")));
        }
        finally
        {
            Stamps.PathOverride = was;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}

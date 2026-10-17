using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The View panel, the help card and the status bar's readouts in another language (the made-up one: PseudoLang),
/// with an area open and objects selected: they show only catalogue texts but for the world's builders (data); the
/// 3D resolution's message is shown in the language while Claude reads its English; and the question before a save
/// is asked in it.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowViewLangTests
{
    [AvaloniaFact]
    public async Task ViewPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using (var r = new WorldWindowRun())
        {
            await r.Open();
            string[] builders = [.. Builders.Players(r.W.Session!.Scene.World!).Select(p => p.Label)];
            Assert.Empty(PseudoLang.Untranslated(r.W.ViewPanelCard, builders));
            Assert.Empty(PseudoLang.Untranslated(r.W.HelpCard));

            // The message names the list's choice in each language.
            r.W.ResolutionBox.SelectedIndex = 2;
            var (width, h) = r.W.View.RenderSize();
            Assert.Equal(PseudoLang.Wrap($"3D resolution: {PseudoLang.Wrap("Fast")}, {width}×{h} pixels."), r.W.MessageText.Text);
            Assert.Equal($"3D resolution: Fast, {width}×{h} pixels.", r.W.LastMessageEn);

            // The status bar with a selection said in it.
            r.W.View.Select([0, 1]);
            var bar = (Control)r.W.MessageText.Parent!;
            Assert.Empty(PseudoLang.Untranslated(bar));
        }

        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        var saving = w.ConfirmSave(Lang.T("common.keepThem"));
        var d = TestWindows.Shown(w);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        Assert.False(await saving);
        w.Close();
    }
}

using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Paste panel in another language (the made-up one: PseudoLang): with nothing copied, then with objects copied,
/// stacked, turned and mirrored, and with a blueprint's name, it shows only catalogue texts.
/// </summary>
[Collection("DataDir")]
public sealed class PastePanelLangTests
{
    [AvaloniaFact]
    public void PastePanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var w = new MainWindow(load: false) { Width = 1400, Height = 900 };
        w.Show();
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(40, 30, 40), Vector3.Zero, 1, false));
        w.View.Show(s.Scene, null);
        w.Edit(s);
        var p = w.PastePanel;
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        w.Tools.ChooseSelect();
        w.View.Select([0]);
        w.Copy();
        w.StartPaste();
        p.CopiesBox.Value = 3;
        p.MirrorButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        p.TurnButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        w.View.Paste.Clip!.Name = "My hut";
        p.Refresh();
        Assert.StartsWith(PseudoLang.Open + "My hut: ", p.Info.Text, StringComparison.Ordinal);
        Assert.Empty(PseudoLang.Untranslated(p.Card, "My hut"));
    }
}

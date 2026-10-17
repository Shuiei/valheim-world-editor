using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop's Build panel in another language (the made-up one: PseudoLang): each tab of pieces, a search that
/// finds none, and its Library filled and searched for nothing show only catalogue texts. Pieces, prefabs, stations
/// and materials are game data, the same in every language.
/// </summary>
[Collection("DataDir")]
public sealed class BuildPanelLangTests
{
    [AvaloniaFact]
    public async Task BuildPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall");
        var p = (await r.OpenWorkshop()).BuildPanel;
        var pieces = BuildPanel.Pieces;
        string[] data =
        [
            .. pieces.SelectMany(x => new[] { x.Prefab, x.Name }),
            .. pieces.Select(x => PieceCost.Get(x.Prefab)?.Station).OfType<string>(),
            .. PieceCost.Of(pieces.Select(x => x.Prefab)).Materials.Select(m => m.Name),
            "Gate",
        ];
        // The generator below the Library is the Dungeon panel's, checked with it. Its texts are read again each
        // time: it starts making a dungeon on its own and then shows another line.
        List<string> Shown()
        {
            var generator = PseudoLang.Untranslated(p.Dungeon.View, data).Concat(PseudoLang.Untranslated(p.Dungeon.Actions, data)).ToHashSet();
            return [.. PseudoLang.Untranslated(p.Card, data).Where(t => !generator.Contains(t))];
        }

        foreach (var tab in p.Card.GetLogicalDescendants().OfType<Button>().Where(b => b.Tag is int).ToList())
        {
            tab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(Shown());
        }
        p.Search.Text = "no piece is called this";
        Assert.Empty(Shown());
        p.Search.Text = "";

        p.ShowLibrary(true);
        Assert.Contains(p.LibraryList.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Gate");
        Assert.Empty(Shown());
        p.LibrarySearch.Text = "no blueprint is called this";
        Assert.Empty(Shown());
    }
}

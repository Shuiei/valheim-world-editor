using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The dock's pieces picked last: a tile clicked goes first (once), no more than MaxRecent are kept, they are kept between
/// runs, a piece the game no longer has is left out, and a click on one picks it again.
/// </summary>
public sealed class BuildPanelRecentTests
{
    /// <summary>Pieces picked last as a run kept them: one the game no longer has, an empty name, the wall twice.</summary>
    private static readonly string[] Kept = ["zz_gone_piece", "woodwall", "", "woodwall"];

    /// <summary>A button clicked.</summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>The prefabs the row of pieces picked last shows.</summary>
    private static List<string> Shown(BuildPanel p) => [.. p.RecentRow.Children.OfType<Button>().Select(b => (string)b.Tag!)];

    [AvaloniaFact]
    public void APieceClickedGoesFirstAndOnce()
    {
        var w = new MainWindow(load: false);
        var p = w.BuildPanel;
        p.Remember(Prefs.InMemory());
        Assert.Empty(Shown(p));
        Assert.Contains(p.RecentRow.Children.OfType<TextBlock>(), t => t.Text == "The pieces you pick show here.");

        p.Search.Text = "wall";
        var tiles = p.Tiles.Children.OfType<Button>().Take(2).ToList();
        Click(tiles[0]);
        Click(tiles[1]);
        Click(tiles[0]);

        Assert.Equal([(string)tiles[0].Tag!, (string)tiles[1].Tag!], Shown(p));
        Assert.Equal([(string)tiles[0].Tag!], w.PlaceTool.Chosen);
        Assert.Contains("on", p.RecentRow.Children.OfType<Button>().First().Classes);
        // Picked again from the row: chosen, and the tiles lit to match.
        Click(p.RecentRow.Children.OfType<Button>().Last());
        Assert.Equal([(string)tiles[1].Tag!], w.PlaceTool.Chosen);
        Assert.Equal((string)tiles[1].Tag!, Shown(p)[0]);
    }

    [AvaloniaFact]
    public void NoMoreThanMaxRecentAreKeptBetweenRuns()
    {
        var prefs = Prefs.InMemory();
        var p = new MainWindow(load: false).BuildPanel;
        p.Remember(prefs);
        var pieces = BuildPanel.Pieces.Select(x => x.Prefab).Take(BuildPanel.MaxRecent + 3).ToList();
        foreach (string prefab in pieces)
        {
            p.Pick(prefab);
        }

        Assert.Equal(BuildPanel.MaxRecent, p.Recent.Count);
        Assert.Equal(pieces[^1], p.Recent[0]);
        Assert.Equal(p.Recent, prefs.Get<string[]>("build.recent", []));
        var again = new MainWindow(load: false).BuildPanel;
        again.Remember(prefs);
        Assert.Equal(p.Recent, again.Recent);
    }

    [AvaloniaFact]
    public void APieceTheGameNoLongerHasIsLeftOut()
    {
        var prefs = Prefs.InMemory();
        prefs.Set("build.recent", Kept);
        var p = new MainWindow(load: false).BuildPanel;
        p.Remember(prefs);

        Assert.Equal(["zz_gone_piece", "woodwall"], p.Recent);
        Assert.Equal(["woodwall"], Shown(p));
    }
}

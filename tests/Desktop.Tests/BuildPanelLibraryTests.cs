using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop's Build panel: pieces listed by the station they need, the Library searched by name, description and
/// tags (saying when nothing matches or there is nothing yet), a blueprint whose picture cannot be read still listed,
/// and a piece search matching nothing.
/// </summary>
public sealed class BuildPanelLibraryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-library-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("Workbench", 1)]
    [InlineData("stonecutter", 2)]
    [InlineData("Forge", 3)]
    [InlineData("Artisan table", 4)]
    [InlineData("Black forge", 5)]
    [InlineData("Galdr table", 6)]
    [InlineData("Some mod's table", 7)]
    public void PiecesAreRankedByTheStationTheyNeed(string? station, int rank) => Assert.Equal(rank, BuildPanel.StationRank(station));

    /// <summary>The Library's lines: its rows, or the text it shows instead.</summary>
    private static string? Said(BuildPanel p) => p.LibraryList.Children.Count == 1 && p.LibraryList.Children[0] is TextBlock t ? t.Text : null;

    [AvaloniaFact]
    public void TheLibraryIsSearchedByNameDescriptionAndTags()
    {
        var w = new MainWindow(load: false);
        var p = w.BuildPanel;
        p.Folder = () => _dir;
        // No folder yet: nothing to list.
        p.RenderLibrary();
        Assert.StartsWith("No blueprints yet", Said(p));
        Directory.CreateDirectory(_dir);
        var clip = CopyFormat.ToJson(BlueprintsPanelRun.Clip("Hut", "woodwall"));
        File.WriteAllText(Path.Combine(_dir, Homestead.FileName("Hut")), Homestead.Write(clip, "Hut", "test", null, DateTime.Now, "A cosy place by the lake", ["cabin", "viking"]));
        File.WriteAllText(Path.Combine(_dir, Homestead.FileName("Gate")), Homestead.Write(clip, "Gate", "test", null, DateTime.Now));
        // Its picture is no picture: listed all the same.
        File.WriteAllText(Path.Combine(_dir, "Gate.png"), "not a picture");
        p.RenderLibrary();
        Assert.Equal(2, p.LibraryList.Children.Count);
        Assert.All(p.LibraryList.Children, c => Assert.IsType<Border>(c));
        // By a word of the description, by a tag, by name and tag at once.
        p.LibrarySearch.Text = "lake";
        Assert.Single(p.LibraryList.Children);
        p.LibrarySearch.Text = "VIKING";
        Assert.Single(p.LibraryList.Children);
        p.LibrarySearch.Text = "hut cabin";
        Assert.Single(p.LibraryList.Children);
        p.LibrarySearch.Text = "castle";
        Assert.Equal("Nothing matches.", Said(p));
        p.LibrarySearch.Text = "";
        Assert.Equal(2, p.LibraryList.Children.Count);
    }

    [AvaloniaFact]
    public void APieceSearchMatchingNothingSaysSo()
    {
        var w = new MainWindow(load: false);
        var p = w.BuildPanel;
        p.Search.Text = "zzz no such piece";
        var said = Assert.IsType<TextBlock>(Assert.Single(p.Tiles.Children));
        Assert.Equal("No piece matches.", said.Text);
        p.Search.Text = "wall";
        // Pieces (under their stations' headings), not the word that none matches.
        Assert.Contains(p.Tiles.Children, c => c is not TextBlock);
        Assert.DoesNotContain(p.Tiles.Children, c => c is TextBlock { Text: "No piece matches." });
    }
}

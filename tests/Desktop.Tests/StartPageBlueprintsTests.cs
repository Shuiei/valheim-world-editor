using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page's tiles: the Workshop's blueprints newest first (with their picture, or the icon when it is missing or
/// cannot be read, and when they were saved in words), a hint when there are none, and Home mixing recent worlds and
/// blueprints by date, each opening what it shows. Homestead's folder and the saved servers are the test's own; it runs
/// alone.
/// </summary>
[Collection("DataDir")]
public sealed class StartPageBlueprintsTests : IDisposable
{
    /// <summary>A 1 × 1 PNG picture.</summary>
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-start-bp-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _folder = Homestead.FolderOverride, _servers = ServerConfig.PathOverride, _settings = AppSettings.PathOverride;

    public StartPageBlueprintsTests()
    {
        Directory.CreateDirectory(_dir);
        Homestead.FolderOverride = Path.Combine(_dir, "blueprints");
        ServerConfig.PathOverride = Path.Combine(_dir, "servers.cfg");
        AppSettings.PathOverride = Path.Combine(_dir, "settings.json");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Homestead.FolderOverride = _folder;
        ServerConfig.PathOverride = _servers;
        AppSettings.PathOverride = _settings;
        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>
    /// A blueprint of two pieces in Homestead's folder, last written that many days ago; its path.
    /// </summary>
    private static string Blueprint(string name, int daysAgo, byte[]? picture = null)
    {
        Directory.CreateDirectory(Homestead.Folder());
        string path = Path.Combine(Homestead.Folder(), Homestead.FileName(name));
        File.WriteAllText(path, Homestead.Write(CopyFormat.ToJson(BlueprintsPanelRun.Clip(name, "wood_floor", "woodwall")), name, "test", null, DateTime.Now));
        File.SetLastWriteTime(path, DateTime.Now.AddDays(-daysAgo));
        if (picture != null)
        {
            File.WriteAllBytes(Path.ChangeExtension(path, ".png"), picture);
        }
        return path;
    }

    /// <summary>A tile's name and the line under it.</summary>
    private static (string Name, string Line) Words(Control tile)
    {
        var texts = ((StackPanel)((StackPanel)((Button)tile).Content!).Children[1]).Children.OfType<TextBlock>().ToList();
        return (texts[0].Text!, texts[1].Text!);
    }

    /// <summary>What a tile shows on top: a picture, or the kind's icon.</summary>
    private static Control Top(Control tile) => ((Border)((StackPanel)((Button)tile).Content!).Children[0]).Child!;

    [AvaloniaFact]
    public void TheWorkshopListsTheBlueprintsNewestFirstWithWhenTheyWereSaved()
    {
        var p = new StartPage(new AppSettings());
        // None yet: a hint.
        p.SetMode("workshop");
        Assert.IsType<TextBlock>(Assert.Single(p.BlueprintTiles.Children));
        string hut = Blueprint("Hut", 0, Png);
        Blueprint("Shed", 1, [1, 2, 3]);
        Blueprint("Barn", 5);
        Blueprint("Hall", 40);
        p.FillBlueprints();
        var tiles = p.BlueprintTiles.Children.ToList();
        Assert.Equal(["Hut", "Shed", "Barn", "Hall"], tiles.Select(t => Words(t).Name));
        Assert.Equal("2 pieces · today", Words(tiles[0]).Line);
        Assert.Equal("2 pieces · yesterday", Words(tiles[1]).Line);
        Assert.Equal("2 pieces · 5 days ago", Words(tiles[2]).Line);
        Assert.Equal("2 pieces · " + DateTime.Now.AddDays(-40).ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), Words(tiles[3]).Line);
        // Its picture; a picture that cannot be read and none show the icon.
        Assert.IsType<Image>(Top(tiles[0]));
        Assert.IsType<Border>(Top(tiles[1]));
        Assert.IsType<Border>(Top(tiles[2]));
        // A click opens it in the Workshop.
        string? asked = null;
        p.WorkshopRequested += path => asked = path;
        tiles[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(hut, asked);
    }

    [AvaloniaFact]
    public void HomeMixesRecentWorldsAndBlueprintsByDate()
    {
        // The test world, as a saved world opened two days ago.
        string world = WorldEditor.Tests.Fixtures.CopyFixture();
        var settings = new AppSettings
        {
            Recent =
            [
                new(world, "CITest", DateTime.Now.AddDays(-2)),
                // Gone since: not shown.
                new(Path.Combine(_dir, "Gone"), "Gone", DateTime.Now),
            ],
        };
        string hut = Blueprint("Hut", 0);
        Blueprint("Hall", 10);
        try
        {
            var p = new StartPage(settings);
            var opened = new List<string>();
            p.OpenRequested += (_, what) => opened.Add(what);
            string? asked = null;
            p.WorkshopRequested += path => asked = path;
            p.FillHome();
            var tiles = p.RecentTiles.Children.ToList();
            Assert.Equal(["Hut", "CITest", "Hall"], tiles.Select(t => Words(t).Name));
            Assert.Equal("Blueprint · 2 pieces", Words(tiles[0]).Line);
            Assert.Equal("Saved world · 2 days ago", Words(tiles[1]).Line);
            tiles[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(hut, asked);
            tiles[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(["Opening the world…"], opened);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(world)!, recursive: true);
        }
    }
}

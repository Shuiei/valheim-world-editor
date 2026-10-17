using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;
using static TerrainEditor.Desktop.Tests.StartPageRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page's saved worlds as cards: the cards and the none-found line, Refresh, a look overtaken by a later one,
/// a folder that cannot be read, and a world added or removed while the page is open.
/// </summary>
[Collection("DataDir")]
public class StartPageWorldsTests
{
    [AvaloniaFact]
    public async Task SavedWorldsAreCardsAndNoneIsSaid()
    {
        using var r = new Run();
        r.P.FindWorlds = _ => [];
        r.P.FillWorlds();
        await r.P.WorldsCheck!;
        Assert.Contains("No worlds found on this computer", Texts(r.P.WorldCards));
        string dir = Fixtures.CopyFixture();
        try
        {
            r.P.FindWorlds = _ =>
            [
                new Worlds.Info("CITest", dir, DateTime.Now, 2, "a test folder", true, null),
                new Worlds.Info("Broken", "/nowhere", DateTime.Now, 0, "elsewhere", false, "No save files in it."),
            ];
            r.P.FillWorlds();
            await r.P.WorldsCheck!;
            var cards = r.P.WorldCards.Children.OfType<Button>().ToList();
            Assert.Equal(2, cards.Count);
            Assert.False(cards[1].IsEnabled);
            Assert.Contains("No save files in it.", Texts(r.P.WorldCards));
            Click(cards[0]);
            Assert.Equal(new[] { "Opening the world…" }, r.Opened);
            Assert.Equal("offline", r.Settings.LastMode);
            Assert.Equal(Path.GetFullPath(dir), r.Settings.Recent[0].Path);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, true);
        }
    }

    [AvaloniaFact]
    public async Task RefreshFindsTheWorldsAgain()
    {
        using var r = new Run();
        List<Worlds.Info> found = [];
        r.P.FindWorlds = _ => found;
        r.P.SetMode("offline");
        await r.P.WorldsCheck!;
        Assert.Contains("No worlds found on this computer", Texts(r.P.WorldCards));
        // A world folder added while the page is open shows after Refresh, and goes once removed.
        found = [new Worlds.Info("Added", "/somewhere", DateTime.Now, 1, "a test folder", true, null)];
        Click(r.P.RefreshWorlds);
        await r.P.WorldsCheck!;
        Assert.Single(r.P.WorldCards.Children.OfType<Button>());
        Assert.Contains("Added", Texts(r.P.WorldCards));
        found = [];
        Click(r.P.RefreshWorlds);
        await r.P.WorldsCheck!;
        Assert.Empty(r.P.WorldCards.Children.OfType<Button>());
        Assert.Contains("No worlds found on this computer", Texts(r.P.WorldCards));
    }

    [AvaloniaFact]
    public async Task ALookOvertakenByALaterOneShowsNothing()
    {
        using var r = new Run();
        var older = new Worlds.Info("Older", "/older", DateTime.Now, 1, "a test folder", true, null);
        var newer = new Worlds.Info("Newer", "/newer", DateTime.Now, 1, "a test folder", true, null);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool slow = true;
        r.P.FindWorlds = _ =>
        {
            if (!slow)
            {
                return [newer];
            }
            started.Set();
            release.Wait();
            return [older];
        };
        // A slow look starts, then the page lists the worlds again before it ends.
        var check = r.P.CheckWorlds();
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        slow = false;
        r.P.SetMode("offline");
        await r.P.WorldsCheck!;
        Assert.Contains("Newer", Texts(r.P.WorldCards));
        // The slow look ends: its older worlds do not replace the newer list.
        release.Set();
        await check;
        Assert.Contains("Newer", Texts(r.P.WorldCards));
        Assert.DoesNotContain("Older", Texts(r.P.WorldCards));
    }

    [AvaloniaFact]
    public async Task AFolderThatCannotBeReadLeavesThePageWhole()
    {
        using var r = new Run();
        r.P.FindWorlds = _ => throw new IOException("a drive that went away");
        r.P.SetMode("offline");
        await r.P.WorldsCheck!;
        Assert.Equal("offline", r.P.Mode);
        Assert.True(r.P.OfflinePanel.IsVisible);
    }

    [AvaloniaFact]
    public async Task AWorldAddedOrRemovedShowsByItself()
    {
        using var r = new Run();
        List<Worlds.Info> found = [new Worlds.Info("First", "/first", DateTime.Now, 1, "a test folder", true, null)];
        r.P.FindWorlds = _ => found;
        r.P.SetMode("offline");
        await r.P.WorldsCheck!;
        var first = Assert.Single(r.P.WorldCards.Children.OfType<Button>());
        // Nothing changed: the cards stay as they are (no redraw under the mouse).
        await r.P.CheckWorlds();
        Assert.Same(first, Assert.Single(r.P.WorldCards.Children.OfType<Button>()));
        // A world folder added, then one removed, shows without Refresh.
        found = [.. found, new Worlds.Info("Second", "/second", DateTime.Now, 1, "a test folder", true, null)];
        await r.P.CheckWorlds();
        Assert.Equal(2, r.P.WorldCards.Children.OfType<Button>().Count());
        Assert.Contains("Second", Texts(r.P.WorldCards));
        found = [found[1]];
        await r.P.CheckWorlds();
        Assert.DoesNotContain("First", Texts(r.P.WorldCards));
        // The same world under another name, or found elsewhere (a recent world now): its card says so.
        found = [found[0] with { Name = "Renamed", Where = "recent" }];
        await r.P.CheckWorlds();
        Assert.Contains("Renamed", Texts(r.P.WorldCards));
        Assert.Contains("recent", Texts(r.P.WorldCards));
        // Another page shows: the worlds are not drawn behind it.
        r.P.SetMode("home");
        found = [];
        await r.P.CheckWorlds();
        Assert.Contains("Renamed", Texts(r.P.WorldCards));
    }
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;
using static TerrainEditor.Desktop.Tests.StartPageRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page's game panel: a game whose plugin settings cannot be read keeps what it shows, and a game that cannot
/// be checked says why until it can be; a Recent world that cannot be opened says why on Home; a log or page the system
/// opens nothing for is named at the top.
/// </summary>
[Collection("DataDir")]
public class StartPageGameTests
{
    [AvaloniaFact]
    public async Task MyGameWithUnreadablePluginSettingsKeepsWhatItShows()
    {
        // Root (CI's containers) reads a file whatever its permissions: nothing to check there.
        if (!OperatingSystem.IsLinux() || Environment.UserName == "root")
        {
            return;
        }
        using var r = new Run();
        string bep = Path.Combine(r.Temp, "BepInEx");
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        string cfg = Path.Combine(bep, "config", LocalGame.ConfigName);
        File.WriteAllText(cfg, $"Port = {FakeGame.FreePort()}\nToken = t\n");
        r.Settings.BepInExFolders.Add(r.Temp);
        await r.P.PollGame();
        object? shown = r.P.GameState.Content;
        Assert.Contains("Start Valheim and load your world", Texts((Control)shown!));
        // The game is now running, but its settings cannot be read: the page stays as it was.
        using var game = new FakeGame();
        File.WriteAllText(cfg, $"Port = {game.Port}\nToken = {game.Token}\n");
        File.SetUnixFileMode(cfg, UnixFileMode.None);
        try
        {
            await r.P.PollGame();
            Assert.Same(shown, r.P.GameState.Content);
        }
        finally
        {
            File.SetUnixFileMode(cfg, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [AvaloniaFact]
    public async Task AGameThatCannotBeCheckedSaysWhyUntilItCanBe()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        using var r = new Run();
        string config = Path.Combine(r.Temp, "BepInEx", "config");
        Directory.CreateDirectory(config);
        string cfg = Path.Combine(config, LocalGame.ConfigName);
        // A link to nothing: there as a file, but it cannot be read (even by root).
        File.CreateSymbolicLink(cfg, Path.Combine(r.Temp, "missing.cfg"));
        r.Settings.BepInExFolders.Add(r.Temp);
        await r.P.PollGame();
        Assert.Contains("Your game could not be checked", Texts(r.P.GamePanel));
        // Checked again the same way: said once.
        await r.P.PollGame();
        Assert.Contains("Your game could not be checked", Texts(r.P.GamePanel));
        // Readable again: the failure goes.
        File.Delete(cfg);
        File.WriteAllText(cfg, $"Port = {FakeGame.FreePort()}\nToken = t\n");
        await r.P.PollGame();
        Assert.DoesNotContain("Your game could not be checked", Texts(r.P.GamePanel));
        Assert.Contains("Start Valheim and load your world", Texts((Control)r.P.GameState.Content!));
    }

    // A Recent tile whose folder holds no world any more: Home says why (it was said on A saved world, not shown).
    [AvaloniaFact]
    public async Task ARecentWorldThatCannotBeOpenedSaysWhyOnHome()
    {
        using var r = new Run();
        string gone = Path.Combine(r.Temp, "Gone");
        Directory.CreateDirectory(gone);
        r.Settings.Recent.Add(new AppSettings.RecentWorld(gone, "Gone", DateTime.Now));
        await r.P.FillHome();
        Click(r.P.RecentTiles.Children.OfType<Button>().Single(b => Texts(b).Contains("Gone", StringComparison.Ordinal)));
        Assert.Empty(r.Opened);
        Assert.Contains("No Valheim world in that folder", Texts(r.P.HomePanel));
        Assert.Equal("", r.P.PathError.Text ?? "");
    }

    [AvaloniaFact]
    public async Task ALinkTheSystemOpensNothingForIsNamedAtTheTop()
    {
        var page = new StartPage(new AppSettings());
        try
        {
            page.OpenUrl = _ => Task.FromResult(true);
            Assert.True(await page.OpenLog());
            Assert.False(page.LastError.IsVisible);
            page.OpenUrl = _ => Task.FromResult(false);
            Assert.False(await page.OpenOrSay(new Uri("https://example.com/")));
            Assert.True(page.LastError.IsVisible);
            Assert.Contains("https://example.com/", page.LastError.Text);
            Assert.False(await page.OpenLog());
            Assert.Contains("ValheimWorldEditor.log", page.LastError.Text);
        }
        finally
        {
            page.Stop();
        }
    }
}

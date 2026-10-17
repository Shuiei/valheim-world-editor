using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>
    /// Greys out every action while the game's files are read (LookBanner.Busy), and gives them back once it is done:
    /// what is opened meanwhile would miss the game's look. The side menu still moves between the pages.
    /// </summary>
    private void ShowBusy()
    {
        foreach (var c in _busyOff)
        {
            c.IsEnabled = !LookBanner.Busy;
            c.Opacity = LookBanner.Busy ? 0.45 : 1;
        }
    }

    /// <summary>
    /// A page's title and what it is for, in the language in use (asked for each time: a page made after the language
    /// changed shows the new one).
    /// </summary>
    /// <param name="mode">The page (a <c>ModeButtons</c> key).</param>
    /// <exception cref="ArgumentOutOfRangeException">No such page.</exception>
    private static (string Title, string Text) PageTexts(string mode) => mode switch
    {
        "home" => (Lang.T("start.page.home"), ""),
        "game" => (Lang.T("start.page.game"), Lang.T("start.page.gameText")),
        "server" => (Lang.T("start.page.server"), Lang.T("start.page.serverText")),
        // The same words as the mode's tip on the left.
        "offline" => (Lang.T("start.page.offline"), Lang.T("tip.start.offline")),
        "workshop" => (Lang.T("start.page.workshop"), Lang.T("start.page.workshopText")),
        "new" => (Lang.T("start.page.new"), Lang.T("start.page.newText")),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "No such start page."),
    };

    /// <summary>
    /// Shows a page ("home", "game", "server", "offline", "workshop", "new") and fills it; the game is polled
    /// on Home and My game only.
    /// </summary>
    public void SetMode(string mode)
    {
        Mode = mode;
        foreach (var (k, b) in ModeButtons)
        {
            b.Classes.Set("on", k == mode);
            b.Background = k == mode ? Ui.Panel2 : Brushes.Transparent;
        }
        (_pageTitle.Text, _pageText.Text) = PageTexts(mode);
        _pageText.IsVisible = _pageText.Text.Length > 0;
        HomePanel.IsVisible = mode == "home";
        GamePanel.IsVisible = mode == "game";
        ServerPanel.IsVisible = mode == "server";
        OfflinePanel.IsVisible = mode == "offline";
        WorkshopPanel.IsVisible = mode == "workshop";
        NewWorldPanel.IsVisible = mode == "new";
        _gameTimer.Stop();
        _worldsTimer.Stop();
        // The game is watched on Home too (it offers to edit it live).
        if (mode is "game" or "home")
        {
            _ = PollGame();
            _gameTimer.Start();
        }
        if (mode == "home")
        {
            _ = FillHome();
        }
        if (mode == "workshop")
        {
            _ = FillBlueprints();
        }
        if (mode == "server")
        {
            FillServers();
        }
        if (mode == "offline")
        {
            FillWorlds();
            _worldsTimer.Start();
        }
    }

    /// <summary>Stops polling the game and the worlds (the page is left).</summary>
    public void Stop()
    {
        _gameTimer.Stop();
        _worldsTimer.Stop();
    }
}

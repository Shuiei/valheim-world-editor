using Avalonia.Controls;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>
    /// One check of the game at a time: the timer does not wait for a slow one, and an older one finishing
    /// last would show its stale state (or error) over the newer one's. Asked during a check, it waits for
    /// it and one more check after (asks during the same check make that one more check together), so the
    /// state shown once it returns is newer than the ask. All on the window's thread.
    /// </summary>
    internal Task PollGame()
    {
        if (_poll is { IsCompleted: false })
        {
            _pollAgain = true;
            return _poll;
        }
        return _poll = Checks();

        async Task Checks()
        {
            do
            {
                _pollAgain = false;
                await CheckGame();
            }
            while (_pollAgain);
        }
    }

    /// <summary>Opens a live world through the bridge, remembering the mode as the last one used.</summary>
    /// <param name="live">The connection to the plugin.</param>
    /// <param name="label">The session's label ("my game", "server " and the host, the address typed).</param>
    /// <param name="mode">The page to come back to next time (LastMode).</param>
    /// <param name="what">What to say while it loads.</param>
    private void OpenLive(LiveBridge live, string label, string mode, string what)
    {
        _settings.LastMode = mode;
        _settings.Save();
        OpenRequested?.Invoke(() => WorldSession.OpenLive(live, label), what);
    }

    /// <summary>Opens the editor's log file with the system; says where it is when the system opens nothing.</summary>
    internal Task<bool> OpenLog() => OpenOrSay(new Uri(Path.GetFullPath(Log.FilePath)));

    /// <summary>
    /// Opens a web page or file with the system; when the system opens nothing (no browser or viewer set up), the line at
    /// the top of the page says what to open by hand, so the click is not lost without a word.
    /// </summary>
    /// <returns>Whether the system opened it.</returns>
    internal async Task<bool> OpenOrSay(Uri address)
    {
        bool opened = await OpenUrl(address);
        if (!opened)
        {
            LastError.Text = Lang.T("start.error.notOpened", address.IsFile ? Ui.Tilde(address.LocalPath) : address.ToString());
            LastError.IsVisible = true;
        }
        return opened;
    }

    /// <summary>
    /// Opens a world folder offline (a leading ~ is the home folder), adding it to Recent; a folder that is no
    /// world says why where it was chosen (<c>PathError</c> unless another line is given).
    /// </summary>
    /// <param name="path">The world's folder.</param>
    /// <param name="error">The line that says why it cannot be opened: Home's for its Recent tiles (A saved world's
    /// line is not shown there).</param>
    internal void OpenFolder(string path, TextBlock? error = null)
    {
        error ??= PathError;
        path = Places.ExpandHome(path);
        if (Worlds.Check(path) is string problem)
        {
            error.Text = problem;
            return;
        }
        error.Text = "";
        string full = Path.GetFullPath(path);
        _settings.LastMode = "offline";
        // Saves the settings, LastMode with them.
        _settings.AddRecent(full, Worlds.WorldName(full));
        OpenRequested?.Invoke(() => Task.Run(() => WorldSession.Open(full)), Lang.T("start.msg.opening"));
    }
}

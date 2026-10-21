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
    /// <param name="label">The session's label ("my game", "server " and the host, "live " and the address).</param>
    /// <param name="mode">The page to come back to next time (LastMode).</param>
    /// <param name="what">What to say while it loads.</param>
    private void OpenLive(LiveBridge live, string label, string mode, string what)
    {
        _settings.LastMode = mode;
        _settings.Save();
        OpenRequested?.Invoke(() => WorldSession.OpenLive(live, label), what);
    }

    /// <summary>Opens the editor's log file with the system.</summary>
    internal Task<bool> OpenLog() => OpenUrl(new Uri(Path.GetFullPath(Log.FilePath)));

    /// <summary>
    /// Opens a world folder offline (a leading ~ is the home folder), adding it to Recent; a folder that is no
    /// world says why in <c>PathError</c>.
    /// </summary>
    internal void OpenFolder(string path)
    {
        path = Places.ExpandHome(path);
        if (Worlds.Check(path) is string problem)
        {
            PathError.Text = problem;
            return;
        }
        PathError.Text = "";
        string full = Path.GetFullPath(path);
        _settings.LastMode = "offline";
        // Saves the settings, LastMode with them.
        _settings.AddRecent(full, Worlds.WorldName(full));
        OpenRequested?.Invoke(() => Task.Run(() => WorldSession.Open(full)), Lang.T("start.msg.opening"));
    }
}

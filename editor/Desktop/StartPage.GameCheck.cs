using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>
    /// Is Valheim running with the plugin? Looks for the plugin's installs and a running game, and shows the state
    /// (running, installed, not started once, no plugin, no BepInEx) with what to do. A failed look shows why on the
    /// card and in the log, the state shown before staying.
    /// </summary>
    private async Task CheckGame()
    {
        List<LocalGame.Bridge> bridges;
        bool bepInEx, plugin;
        LocalGame.Running? running;
        string where, state, key;
        try
        {
            (bridges, bepInEx, plugin) = await Task.Run(() =>
            {
                var found = LocalGame.FindBridges(_settings, out bool b, out bool p);
                return (found, b, p);
            });
            running = await LocalGame.FindRunning(bridges);
            where = running?.Bridge.Where ?? bridges.FirstOrDefault()?.Where ?? "";
            state = running != null ? "running" : bridges.Count > 0 ? "installed" : plugin ? "plugin-not-started" : bepInEx ? "no-plugin" : "no-bepinex";
            key = $"{state}|{running?.World}|{running?.Players}|{where}";
            // Homestead is looked for again only when the game's state changes: it searches every BepInEx's plugins.
            if (key != _gameKey)
            {
                ShowHomestead(await Task.Run(() => Homestead.Find(_settings)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException)
        {
            // Polled every few seconds: the log gets a failure once, not at each poll.
            string why = Lang.T("start.error.check", ex.Message);
            if (_gameError.Text != why)
            {
                Log.Caught(ex, "checking the game on this computer");
                _gameError.Text = why;
            }
            return;
        }
        if (!string.IsNullOrEmpty(_gameError.Text))
        {
            Console.Error.WriteLine("My game: checked again.");
            _gameError.Text = "";
        }
        if (key == _gameKey)
        {
            return;
        }
        _gameKey = key;
        _gameTag.Text = running != null ? Lang.T("start.label.running") : "";
        _running.Content = running == null ? null : RunningCard(running, where);
        GameState.Content = GameContent(running, state, where);
        _gameTimer.Interval = TimeSpan.FromSeconds(running != null ? 5 : 2.5);
    }
}

using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>The reading of the installed mods' pieces under way or done this session; null before the first.</summary>
    private Task? _modScan;

    /// <summary>
    /// Reads the installed mods' pieces (their bundles, kept until they change, and the add-ons' manifests) away from
    /// the window's thread, once a session (<paramref name="again"/>: read their bundles again, Rescan mods); the Build
    /// dock lists them when done. A failure is said in the status bar and the log.
    /// </summary>
    /// <param name="again">Read the mods' bundles again even when they have not changed (Rescan mods).</param>
    internal Task ScanModPieces(bool again = false)
    {
        if (_modScan is { IsCompleted: false } || _modScan != null && !again)
        {
            return _modScan;
        }
        // The tests' copies of the game data: no Valheim, so no mods of its to read.
        if (!GameData.FromGame)
        {
            return Task.CompletedTask;
        }
        BuildPanel.ShowScanning(true);
        var settings = Settings;
        return _modScan = Task.Run(() =>
        {
            ModManifests.Refresh(settings);
            return GameData.ScanMods(settings, again);
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            BuildPanel.ShowScanning(false);
            if (t.Exception?.GetBaseException() is { } e)
            {
                Log.Caught(e, "reading the installed mods' pieces (none listed)");
                Msg(Said.Of("build.msg.scanFailed", e.Message));
            }
            else if (again)
            {
                Msg(Said.Of("build.msg.scanned", t.Result));
            }
        }), TaskScheduler.Default);
    }
}

using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The start page: how to edit, and which world.
    /// </summary>
    /// <param name="error">Shown on a newly made start page; null to keep the page made before.</param>
    internal void ShowStart(string? error = null)
    {
        MapPage?.Stop();
        if (StartPage == null || error != null)
        {
            StartPage?.Stop();
            StartPage = new StartPage(Settings, error);
            StartPage.OpenRequested += async (open, what) => await OpenWorld(open, what);
            StartPage.WorkshopRequested += async path => await OpenWorkshop(path);
            StartPage.NewWorldRequested += ShowNewWorld;
            StartPage.OpenUrl = Launcher.LaunchUriAsync;
            StartPage.SettingsRequested += async () =>
            {
                if (await SettingsDialog.Show(this, Settings))
                {
                    StartPage!.SetMode(StartPage.Mode);
                    // Claude's connection, as the settings now say.
                    await ClaudeServer.Apply(Settings, this);
                }
            };
            StartPage.Confirm = text => Dialogs.Ask(this, "Valheim World Editor", text, Lang.T("common.yes"));
            StartPage.PickFolder = title => Pickers.Folder(StorageProvider, title);
            StartPage.PickFile = title => Pickers.File(StorageProvider, title);
        }
        else
        {
            StartPage.SetMode(StartPage.Mode);
        }
        Title = $"Valheim World Editor {BuildInfo.Version}";
        _pages.Content = StartPage.View;
    }

    /// <summary>
    /// Opens a world (offline or live), then shows its map; back to the start page, with why, when it cannot be opened.
    /// </summary>
    /// <param name="open">Opens it (off the UI thread).</param>
    /// <param name="what">What the busy veil says meanwhile.</param>
    internal async Task OpenWorld(Func<Task<WorldSession>> open, string what)
    {
        Busy(what);
        try
        {
            World = await open();
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "opening a world");
            Busy(null);
            Tunnel.Close();
            ShowStart(Lang.T("world.msg.openFailed", ex.Message));
            return;
        }
        finally
        {
            Busy(null);
        }
        StartPage?.Stop();
        ShowMap();
    }

    /// <summary>
    /// The world map (what is not saved stays pending).
    /// </summary>
    internal void ShowMap()
    {
        if (World == null)
        {
            ShowStart();
            return;
        }
        _navigation++;
        View.SelectTool.Commit();
        KeepHistory();
        _playersTimer.Stop();
        _labelsTimer.Stop();
        // Following the game is the open area's: the map reads the world as it is (Reload for the rest).
        _gameTimer.Stop();
        _gameFetched = null;
        PlayerLabels.Children.Clear();
        if (MapPage == null)
        {
            MapPage = new MapPage
            {
                StartView = () => Prefs.TryGet("map.view", out float[]? v) && v is { Length: 3 } && v.All(float.IsFinite) && v[2] > 0 ? (v[0], v[1], Math.Clamp(v[2], 0.25f, 40f)) : null
            };
            MapPage.Map.ViewChanged += () => Prefs.Set("map.view", new[] { MapPage.Map.Center.X, MapPage.Map.Center.Y, MapPage.Map.MetersPerPixel });
            MapPage.BackToWorlds += async () => await LeaveWorld();
            MapPage.EditRequested += async (x, z, size) => await EditArea(x, z, size);
            MapPage.SaveRequested += async () => await SaveWorld();
            MapPage.DiscardRequested += async () => await DiscardWorld();
            MapPage.ReloadRequested += async () => await ReloadWorld();
            MapPage.Confirm = text => Dialogs.Ask(this, "Valheim World Editor", text, Lang.T("common.yes"));
            MapPage.Tell = text => Dialogs.Tell(this, "Valheim World Editor", text);
        }
        Title = $"{World.World.Name} · Valheim World Editor {BuildInfo.Version}";
        MapPage.Show(World);
        _pages.Content = MapPage.View;
    }
}

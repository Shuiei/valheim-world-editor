using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The world given on the command line (--world, --zone, --size) opened straight in the 3D editor; one that cannot be
    /// opened is said where the editor shows how it loaded (in the language in use) and on the console (in English).
    /// </summary>
    /// <param name="world">The world's folder or name (null: the first one found).</param>
    /// <param name="zoneX">The middle zone, x.</param>
    /// <param name="zoneZ">The middle zone, z.</param>
    /// <param name="size">The area, in zones each way.</param>
    internal async Task OpenDirect(string? world, int zoneX, int zoneZ, int size)
    {
        try
        {
            var session = await Task.Run(() => WorldSession.Open(WorldScene.FindWorld(world)));
            World = session;
            var scene = await Task.Run(() => WorldScene.Load(session, zoneX, zoneZ, size));
            await ShowEditor(scene);
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "opening the world given on the command line");
            LoadedInfo.Text = Lang.T("world.msg.openFailed", ex.Message);
        }
    }

    /// <summary>
    /// The window is open: with <paramref name="load"/>, the game's look is checked, Claude's connection applied and
    /// the models read; then the world given on the command line opens (or the start page shows), and the test
    /// driver starts when asked.
    /// </summary>
    /// <param name="load">False (tests): nothing is loaded.</param>
    private async Task OnOpenedAsync(bool load)
    {
        if (!load)
        {
            return;
        }
        Options.Say("window open");
        // The game's look: copied from the player's Valheim when missing or after a game update
        // (when driven by the tests, only whether it is there: they never copy from the game).
        GameLook.Check(Settings);
        // Claude's connection, when chosen in Settings.
        await ClaudeServer.Apply(Settings, this);
        if (!Options.Driver)
        {
            // The copy older editors made of the game's look: nothing reads it any more.
            _ = Task.Run(GameLook.DeleteOldCopy);
            _modelsLoading = Task.Run(ModelStore.Open);
        }
        if (Options.Direct)
        {
            // --world (and --zone): that area in the 3D editor at once.
            await OpenDirect(Options.World, Options.ZoneX, Options.ZoneZ, Options.Size);
        }
        else
        {
            ShowStart();
            // A world folder or a live game given on the command line: as if chosen on the start page.
            if (Options.LiveUrl is string url)
            {
                StartPage!.SetMode("server");
                StartPage.LiveUrl.Text = url;
                StartPage.LiveToken.Text = Options.LiveToken ?? "";
                await StartPage.ConnectUrl();
            }
            else if (Options.Folder is string folder)
            {
                StartPage!.SetMode("offline");
                StartPage.OpenFolder(folder);
            }
        }
        if (Options.Driver)
        {
            Driver.Start(this);
        }
    }
}

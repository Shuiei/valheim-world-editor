using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Map: Discard, after asking: the unsaved changes dropped, the world read again and the status bar emptied.
    /// </summary>
    internal async Task DiscardWorld()
    {
        if (World is not { } w
            || !await Ask(Lang.T("common.discard"), w.IsLive ? Lang.T("save.ask.discardWorldLive.text", Describe(w.Pending)) : Lang.T("save.ask.discardWorld.text", Describe(w.Pending)), Lang.T("common.discard"), Lang.T("common.keepThem")))
        {
            return;
        }
        Busy(Lang.T("save.msg.reading"));
        try
        {
            await Task.Run(w.Discard);
            // What the status bar said was about the changes dropped (live, the history kept from earlier too): not
            // shown again in the next area.
            Msg(Said.Empty);
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "discarding the changes (reading the world again)");
            Say(Said.Of("save.msg.couldNotRead", ex.Message));
        }
        finally
        {
            Busy(null);
        }
        MapPage?.Show(w);
    }

    /// <summary>
    /// Map, live: reads the world from the game again.
    /// </summary>
    internal async Task ReloadWorld()
    {
        if (World is not { IsLive: true } w)
        {
            return;
        }
        if (await ReloadFromGame(w) is { } failed)
        {
            Say(failed);
        }
        MapPage?.Show(w);
    }

    /// <summary>
    /// Live: reads the world from the game again behind the busy veil (the map's Reload and the 3D editor's).
    /// </summary>
    /// <param name="w">The live world.</param>
    /// <returns>Why the game could not be read (unreachable, or the bridge's answer: a refusal, broken JSON); null when read.</returns>
    private async Task<Said?> ReloadFromGame(WorldSession w)
    {
        Busy(Lang.T("save.msg.loadingLive"));
        try
        {
            await w.Reload();
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            Log.Caught(ex, "reading the world from the game again");
            return Said.Of("save.msg.unreachable", ex.Message);
        }
        finally
        {
            Busy(null);
        }
    }
}

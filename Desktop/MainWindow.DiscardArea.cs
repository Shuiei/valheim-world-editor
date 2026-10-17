using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    // ---- Discard and Reload, here in the editor.

    /// <summary>
    /// Undoes the steps not saved or applied yet, keeping the view, the tool and the rest of the history.
    /// Pending changes with no step here (made in another area, or before) need the world read again.
    /// </summary>
    internal async Task DiscardHere()
    {
        if (Session is not { } s)
        {
            return;
        }
        View.SelectTool.Commit();
        if (s.Pending is (0, 0, 0, 0))
        {
            Msg("top.msg.nothingToDiscard");
            return;
        }
        int steps = s.UnappliedSteps;
        string question = (s.IsLive ? Lang.T("top.ask.discard.textLive", Describe(s.Pending)) : Lang.T("top.ask.discard.text", Describe(s.Pending)))
            + (steps > 0 ? " " + Lang.T("top.ask.discard.steps", steps) : "");
        if (!await Ask(Lang.T("common.discard"), question, Lang.T("common.discard"), Lang.T("common.keepThem")))
        {
            return;
        }
        s.UndoUnapplied();
        UpdateSaveBar();
        History.Refresh();
        if (s.Pending is (0, 0, 0, 0))
        {
            Msg("top.msg.discarded");
            return;
        }
        if (s.Scene.Owner is not { } w || !await Ask(Lang.T("common.discard"), Lang.T("top.ask.readAgain.text"), Lang.T("top.ask.readAgain.yes"), Lang.T("common.keepThem")))
        {
            return;
        }
        Busy(Lang.T("save.msg.reading"));
        try
        {
            await Task.Run(w.Discard);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Caught(ex, "discarding the changes (reading the world again)");
            Msg("save.msg.couldNotRead", ex.Message);
            Busy(null);
            return;
        }
        Busy(null);
        if (await ReopenArea(s.Scene))
        {
            Msg("top.msg.discardedReadAgain");
        }
    }

    /// <summary>Live: the world loaded again from the game (asks first when something waits).</summary>
    internal async Task ReloadHere()
    {
        if (Session is not { IsLive: true, Scene.Owner: { } w } s)
        {
            return;
        }
        if (s.Pending is not (0, 0, 0, 0) && !await Ask(Lang.T("top.label.reload"), Lang.T("top.ask.reload.text"), Lang.T("top.label.reload"), Lang.T("common.keepThem")))
        {
            return;
        }
        if (await ReloadFromGame(w) is { } failed)
        {
            Msg(failed);
            return;
        }
        if (await ReopenArea(s.Scene))
        {
            Msg("top.msg.reloaded");
        }
    }
}

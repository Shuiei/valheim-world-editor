using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Ctrl+Shift+Z: does the last step taken back again (asking first for a step from an earlier session).
    /// </summary>
    internal async Task Redo()
    {
        View.SelectTool.Commit();
        if (Session is { } s && s.RedoList.Count > 0 && await AllowEarlier([s.RedoList[^1]]))
        {
            s.Redo();
        }
        UpdateSaveBar();
    }

    /// <summary>
    /// Steps from an earlier session of the editor (kept on disk), when the world may have changed since (live, or
    /// saved again by the game): asked once per open world before undoing or redoing them.
    /// </summary>
    /// <returns>Whether to go ahead.</returns>
    internal async Task<bool> AllowEarlier(IEnumerable<EditSession.Change> steps)
    {
        if (Session?.Scene.Owner is not { EarlierMayDiffer: true } w || !steps.Any(c => c.Earlier))
        {
            return true;
        }
        string kept = w.Restored!.SavedAt.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
        string question = w.Restored.Replayed ? Lang.T("history.replayedAsk", kept)
            : w.IsLive ? Lang.T("history.ask.earlierLive.text", kept)
            : Lang.T("history.ask.earlier.text", kept);
        if (!await Ask(Lang.T("history.ask.earlier.title"), question, Lang.T("history.ask.earlier.yes"), Lang.T("common.cancel")))
        {
            Msg("history.msg.nothingChanged");
            return false;
        }
        w.EarlierAllowed = true;
        return true;
    }
}

using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Like the web editor's Save: asks first, writes (with a backup), then says what was done. Live, it applies
    /// instead; in the Workshop it saves the blueprint.
    /// </summary>
    internal async Task Save()
    {
        var s = Session;
        // Not while saving (or reading the world): the veil is up.
        if (s == null || IsBusy)
        {
            return;
        }
        if (InWorkshop)
        {
            await SaveWorkshop();
            return;
        }
        View.SelectTool.Commit();
        var (z, d, a, r) = s.Pending;
        if (z + d + a + r == 0)
        {
            await Tell(Lang.T("save.msg.noChanges"));
            return;
        }
        if (s.IsLive)
        {
            await ApplyNow();
            return;
        }
        string what = Describe(s.Pending);
        if (!await ConfirmSave(SaveConfirmText(what, s.Scene.World.Directory)))
        {
            return;
        }
        SaveButton.IsEnabled = false;
        Msg("save.msg.saving");
        // The veil keeps the keys (Ctrl+S, Ctrl+Z), the mouse and Follow off the area while it is written.
        Busy(Lang.T("save.msg.saving"));
        int? newer = null;
        try
        {
            var o = await Task.Run(s.SaveOutcome);
            var res = o.Saved!;
            string msg = o.Message;
            if (res.Skipped.Count > 0)
            {
                msg = Lang.T("save.msg.skipped", msg, string.Join("\n• ", res.Skipped));
            }
            Msg(o.Report);
            // The log stays English.
            Options.Say("Save: " + o.Report.English);
            newer = res.Saved ? null : s.Scene.Owner?.GameSaveSince();
            if (newer == null)
            {
                await Tell(msg);
            }
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "saving the Workshop or area");
            Msg("save.msg.couldNotSave", ex.Message);
        }
        finally
        {
            // Down only once the result has been read: until then Follow and the keys still wait.
            Busy(null);
        }
        // The game saved the world since: the changes can go onto its save.
        if (newer is int game && s.Scene.Owner is { } owner)
        {
            await OfferReplay(owner, game, s.Scene);
        }
        // Saved objects lose their green marks; after a failure the marks and the save bar show what is still unsaved
        // (and the Save button comes back).
        View.RefreshNewMarkers();
        UpdateSaveBar();
    }
}

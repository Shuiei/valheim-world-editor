using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Map: Save (or Apply live) for the whole world; saving asks first, applying only when it would overwrite the
    /// game's changes.
    /// </summary>
    internal async Task SaveWorld()
    {
        if (World is not { } w)
        {
            return;
        }
        if (w.IsLive)
        {
            Busy(Lang.T("save.msg.applying"));
            string message;
            try
            {
                message = (await w.ApplyLive(over => { Busy(null); return ConfirmOverGame(over); })).Message;
            }
            catch (Exception ex)
            {
                Log.Caught(ex, "applying the changes to the running game");
                message = Lang.T("save.msg.couldNotApply", ex.Message);
            }
            finally
            {
                Busy(null);
            }
            await Tell(message);
        }
        else
        {
            string what = Describe(w.Pending);
            if (!await ConfirmSave(SaveConfirmText(what, w.World.Directory)))
            {
                return;
            }
            Busy(Lang.T("save.msg.saving"));
            string msg;
            int? newer = null;
            try
            {
                var o = await Task.Run(w.Save);
                msg = o.Message;
                if (o.Saved is { } r)
                {
                    if (r.Skipped.Count > 0)
                    {
                        msg = Lang.T("save.msg.skipped", msg, string.Join("\n• ", r.Skipped));
                    }
                }
                newer = o.Done ? null : w.GameSaveSince();
            }
            catch (Exception ex)
            {
                Log.Caught(ex, "saving the world");
                msg = Lang.T("save.msg.couldNotSave", ex.Message);
            }
            finally
            {
                Busy(null);
            }
            if (newer is int game)
            {
                await OfferReplay(w, game, null);
            }
            else
            {
                await Tell(msg);
            }
        }
        MapPage?.Show(w);
    }
}

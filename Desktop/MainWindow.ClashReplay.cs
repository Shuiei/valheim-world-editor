using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// A save refused because the game saved the world after the editor read it: offers to bring the pending changes
    /// onto the game's save (WorldSession.Replay) instead of leaving them behind. Asked first; then, for each kind of
    /// change the game made too, whose version stays. Nothing is written: the changes wait on the game's save, to be
    /// checked and saved. The area open is read again.
    /// </summary>
    /// <param name="w">The world.</param>
    /// <param name="newest">The game's save number.</param>
    /// <param name="area">The area open in the 3D editor when Save was pressed there (null: from the map).</param>
    internal async Task OfferReplay(WorldSession w, int newest, WorldScene? area)
    {
        if (!await Ask(Lang.T("replay.title"), Lang.T("replay.offer", newest), Lang.T("replay.bring"), Lang.T("replay.notNow")))
        {
            Say(Said.Of("replay.notNowSaid", newest));
            return;
        }
        Busy(Lang.T("replay.reading"));
        EditReplay? plan;
        try
        {
            plan = await Task.Run(w.PrepareReplay);
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "reading the game's newer save to replay the changes on it");
            Busy(null);
            await Tell(Lang.T("replay.failed", ex.Message));
            return;
        }
        Busy(null);
        if (plan == null)
        {
            await Tell(Lang.T("replay.failed", Lang.T("replay.noNewer")));
            return;
        }
        var keep = new Dictionary<ReplayClash, bool>();
        foreach (var kind in Enum.GetValues<ReplayClash>())
        {
            var clashes = plan.Clashes.Where(c => c.Kind == kind).ToList();
            if (clashes.Count > 0)
            {
                keep[kind] = await Ask(Lang.T("replay.clashTitle"), ClashText(kind, clashes), Lang.T("replay.keepMine"), Lang.T("replay.keepGame"));
            }
        }
        Busy(Lang.T("replay.applying"));
        WorldSession.Outcome o;
        try
        {
            o = await Task.Run(() => w.Replay(plan, k => keep.GetValueOrDefault(k)));
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "replaying the changes on the game's newer save");
            Busy(null);
            await Tell(Lang.T("replay.failed", ex.Message));
            return;
        }
        Busy(null);
        if (o.Reloaded && area != null && View.Scene == area && World == w)
        {
            await ReopenArea(area);
        }
        Msg(o.Report);
        await Tell(o.Message);
    }

    /// <summary>The question for one kind of change the game made too: what it is, the first few of them, and the two ways.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="clashes">The changes of that kind.</param>
    private string ClashText(ReplayClash kind, List<EditReplay.Clash> clashes)
    {
        const int Shown = 8;
        var lines = clashes.Take(Shown).Select(c => c.Kind == ReplayClash.Ground
            ? Lang.T("replay.zone", c.Zone.X, c.Zone.Z)
            : Lang.T("replay.object", NameOfPrefab(c.Prefab) ?? c.Prefab.ToString(System.Globalization.CultureInfo.InvariantCulture), c.Position.X, c.Position.Z)).ToList();
        if (clashes.Count > Shown)
        {
            lines.Add(Lang.T("replay.more", clashes.Count - Shown));
        }
        string list = "• " + string.Join("\n• ", lines);
        string text = kind switch
        {
            ReplayClash.Ground => Lang.T("replay.clash.ground", clashes.Count, list),
            ReplayClash.Changed => Lang.T("replay.clash.changed", clashes.Count, list),
            _ => Lang.T("replay.clash.removed", clashes.Count, list),
        };
        int unsure = clashes.Count(c => c.Unsure);
        return unsure > 0 ? text + "\n\n" + Lang.T("replay.unsure", unsure) : text;
    }
}

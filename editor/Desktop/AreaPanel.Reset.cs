using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// Marks the zones under the selection for reset when saving (after asking), with the Keep my buildings and ground
    /// choices (the map's zone filter has the same two, and asks with the same lines); resetting the ground also
    /// clears their ground edits now.
    /// </summary>
    private async Task ResetZones(EditSession session)
    {
        var zones = Area.ZonesUnder(session.Ground);
        if (zones.Count == 0)
        {
            Say(Said.Of("paste.msg.areaFirst"));
            return;
        }
        bool keep = KeepBuildingsBox.IsChecked == true, ground = ResetGroundBox.IsChecked == true;
        if (!await Confirm(Lang.T("area.ask.reset.text", zones.Count, string.Join(" · ", zones.Select(z => $"{z.X}, {z.Z}"))) + "\n\n"
            + (keep ? Lang.T("reset.ask.removedKeep") : Lang.T("reset.ask.removedAll")) + "\n"
            + (ground ? Lang.T("reset.ask.ground") + "\n" : "") + (session.IsLive ? Lang.T("reset.ask.againLive") : Lang.T("reset.ask.again")))
            || !StillOpen(session))
        {
            return;
        }
        session.MarkResets(zones, keep, ground, Lang.T("area.undo.reset", zones.Count));
        Say(Said.Of("area.msg.resetMarked", zones.Count));
    }

    /// <summary>Unmarks the zones under the selection that are marked for reset.</summary>
    private void CancelReset()
    {
        if (_session() is not { } session)
        {
            return;
        }
        var marked = session.Resets.Select(r => (r.X, r.Z)).ToHashSet();
        int count = Area.ZonesUnder(session.Ground).Count(marked.Contains);
        if (count == 0)
        {
            Say(Said.Of("area.msg.noReset"));
            return;
        }
        session.UnmarkResets(Area.ZonesUnder(session.Ground), Lang.T("area.undo.unreset", count));
        Say(Said.Of("area.msg.unreset", count));
    }

    /// <summary>
    /// Unmarks every zone marked for reset, in the area shown or not (those marked on the map too), as one undo step:
    /// the map's Unmark all.
    /// </summary>
    private void CancelAllResets()
    {
        if (_session() is not { } session)
        {
            return;
        }
        var marked = session.Resets.Select(r => (r.X, r.Z)).ToList();
        if (marked.Count == 0)
        {
            Say(Said.Of("map.msg.noneMarked"));
            return;
        }
        session.UnmarkResets(marked, Lang.T("area.undo.unreset", marked.Count));
        Say(Said.Of("area.msg.unreset", marked.Count));
    }
}

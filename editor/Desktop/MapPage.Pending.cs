using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What is waiting to be saved or applied, and the line that says so.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// Shows what is waiting to be saved or applied; Save and Discard are enabled only when something is.
    /// </summary>
    public void UpdatePending()
    {
        if (_session == null)
        {
            return;
        }
        var pending = _session.Pending;
        Pending.Text = PendingText(pending, _session.IsLive);
        SaveButton.IsEnabled = DiscardButton.IsEnabled = pending != default;
        Map.RequestNextFrameRendering();
    }

    /// <summary>
    /// The pending line: everything saved (or applied, live), or what is not yet.
    /// </summary>
    /// <param name="p">What is waiting: zones of ground, objects deleted and added, zones marked for reset.</param>
    /// <param name="live">A live world (applied rather than saved).</param>
    internal static string PendingText((int Zones, int Deleted, int Added, int Resets) p, bool live)
    {
        string zones = p.Zones > 1 ? Lang.T("map.pending.zones.many", p.Zones) : p.Zones == 1 ? Lang.T("map.pending.zones.one") : "";
        string list = MainWindow.PendingList(zones, p.Deleted, p.Added, p.Resets);
        if (list.Length == 0)
        {
            return live ? Lang.T("map.pending.allApplied") : Lang.T("map.pending.allSaved");
        }
        return live ? Lang.T("map.pending.notApplied", list) : Lang.T("map.pending.notSaved", list);
    }
}

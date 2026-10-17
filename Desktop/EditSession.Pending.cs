using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The line under the Save button: what is pending, as text for the window.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Pending as a line for the window, e.g. "Unsaved: 3 zones, 1 deleted" or "All saved" ("Not applied" and "All
    /// applied" when live).
    /// </summary>
    public string PendingText
    {
        get
        {
            var (z, d, a, r) = Pending;
            var parts = new[]
            {
                z > 1 ? Lang.T("save.zones.many", z) : z > 0 ? Lang.T("save.zones.one", z) : "",
                d > 0 ? Lang.T("save.count.deleted", d) : "",
                a > 0 ? Lang.T("save.count.added", a) : "",
                r > 0 ? Lang.T("save.count.reset", r) : "",
            }.Where(p => p != "").ToList();
            if (parts.Count == 0)
            {
                return IsLive ? Lang.T("save.pending.allApplied") : Lang.T("save.pending.allSaved");
            }
            return IsLive ? Lang.T("save.pending.notApplied", string.Join(", ", parts)) : Lang.T("save.pending.unsaved", string.Join(", ", parts));
        }
    }
}

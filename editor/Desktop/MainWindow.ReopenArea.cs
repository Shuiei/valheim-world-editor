using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The same area again (the world was read again), the view where it was. When it cannot be opened (nor another
    /// area asked for meanwhile), the map (the old area shows the world as it was before), and the failure in a dialog.
    /// </summary>
    /// <returns>Whether the area was opened again.</returns>
    internal async Task<bool> ReopenArea(WorldScene old)
    {
        // The old area shows the world as it was: closed, and not the world's area (its history would be kept).
        Session = null;
        if (old.Owner is { } w && w.Area == old.Session)
        {
            w.Area = null;
        }
        return await OpenBlockKeepingView(old.X0, old.Z0, old.ZonesX, old.ZonesZ, Said.Of("top.msg.reopenFailed"));
    }

    // ---- Live: Apply (the button, and every change when Auto is on). Calls while one runs are merged.

    /// <summary>
    /// Live: Apply (the button, and every change when Auto is on). Calls while one runs are merged into one more run
    /// after it.
    /// </summary>
    internal async Task ApplyNow()
    {
        if (_applying)
        {
            _applyAgain = true;
            return;
        }
        _applying = true;
        try
        {
            do
            {
                _applyAgain = false;
                if (Session is not { IsLive: true } s)
                {
                    break;
                }
                SaveButton.IsEnabled = false;
                Msg("save.msg.applying");
                var o = await s.ApplyLive(ConfirmOverGame);
                // Applied objects lose their green marks.
                View.RefreshNewMarkers();
                Msg(o.Report);
                if (o.Declined)
                {
                    UpdateSaveBar();
                    break;
                }
                UpdateSaveBar();
                History.Refresh();
            }
            while (_applyAgain);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>A change made with Auto on: to the game at once.</summary>
    private void AutoApply(EditSession s)
    {
        if (Settings.AutoApply && s == Session && s.IsLive && !s.Stroking)
        {
            _ = ApplyNow();
        }
    }

    // ---- Live: following the game in the zones shown.
    /// <summary>True while <c>FollowGame</c> runs, so the timer does not start another.</summary>
    private bool _followingGame;

    /// <summary>The world whose plugin is too old to follow (asked again for another world).</summary>
    private WorldSession? _gameTooOld;

    /// <summary>
    /// Read from the game, not merged yet (a stroke was being drawn, an apply was under way): merged at
    /// the next tick, before anything new is read.
    /// </summary>
    private WorldSession.Fetched? _gameFetched;
}

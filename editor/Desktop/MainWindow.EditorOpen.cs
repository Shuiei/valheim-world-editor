using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>An area was opened: the area buttons, the locations note, the players.</summary>
    private void EditorOpened(WorldScene scene)
    {
        AreaNav.IsVisible = scene.Owner != null;
        ShowAreaSize(scene.ZonesX, scene.ZonesZ);
        // The history is this area's now (an open panel showed the last area's).
        History.Refresh();
        int n = Overlays.LocationsNear(scene);
        LocationText.Text = n == 0 ? "" : Lang.T("top.hint.locations", n);
        LocationNote.IsVisible = n > 0;
        _followSaid = Said.Empty;
        // The history kept from an earlier session, said once when it first shows.
        if (scene.Owner is { Restored: { } kept, RestoredSaid: false } owner && scene.Session?.UndoList.Concat(scene.Session.RedoList).Any(c => c.Earlier) == true)
        {
            owner.RestoredSaid = true;
            int steps = scene.Session.UndoList.Count(c => c.Earlier) + scene.Session.RedoList.Count(c => c.Earlier);
            var since = !kept.Changed ? Said.Empty : owner.IsLive ? Said.Of("top.msg.earlierLiveChanged") : Said.Of("top.msg.earlierSaved");
            Msg(Said.Of("top.msg.earlierHistory", kept.SavedAt.ToString("g", System.Globalization.CultureInfo.CurrentCulture), steps) + since);
        }
        bool live = scene.Owner?.IsLive == true;
        PlayersRow.IsVisible = live;
        if (live)
        {
            _ = PollPlayers();
            _playersTimer.Start();
            _labelsTimer.Start();
            _gameTimer.Start();
        }
        else
        {
            _gameTimer.Stop();
            _playersTimer.Stop();
            _labelsTimer.Stop();
            SetPlayers([]);
        }
    }

    // ---- Moving the area. The edits and the history stay with the world, the view comes along.

    /// <summary>
    /// Moves the area by zones (+X east, +Z north), keeping its size and the view; the edits and the history stay
    /// with the world. Not while something unfinished would be lost (MoveBlocker): the status bar says why.
    /// </summary>
    internal async Task MoveArea(int dx, int dz)
    {
        if (View.Scene is not { Owner: not null } s)
        {
            return;
        }
        if (MoveBlocker() is { } why)
        {
            Msg("top.msg.cannotMove", why);
            return;
        }
        await OpenBlockKeepingView(s.X0 + dx, s.Z0 + dz, s.ZonesX, s.ZonesZ);
    }

    /// <summary>
    /// Opens the block of zones from (x0, z0); when it did open, the camera stays where it was in the world.
    /// </summary>
    /// <param name="x0">The south-west zone, east.</param>
    /// <param name="z0">The south-west zone, north.</param>
    /// <param name="zonesX">Zones west to east.</param>
    /// <param name="zonesZ">Zones south to north.</param>
    /// <param name="context">Said before the failure, when it cannot be opened (see <see cref="EditBlock"/>).</param>
    /// <param name="progress">Said in the status bar while it opens ("Opening 5 × 5 zones…"), and taken away once open.</param>
    /// <returns>Whether the block was opened.</returns>
    private async Task<bool> OpenBlockKeepingView(int x0, int z0, int zonesX, int zonesZ, Said? context = null, Said? progress = null)
    {
        if (progress is { } p)
        {
            Msg(p);
        }
        var cam = View.WorldCamera;
        bool opened = await EditBlock(x0, z0, zonesX, zonesZ, context: context);
        if (View.Scene is { } ns && (ns.X0, ns.Z0, ns.ZonesX, ns.ZonesZ) == (x0, z0, zonesX, zonesZ) && cam != null)
        {
            View.WorldCamera = cam;
        }
        // Open: the line said it was still opening. What the area said as it opened (an earlier session's history)
        // stays.
        if (opened && progress is { } said && MessageText.Text == said.Shown)
        {
            Msg(Said.Empty);
        }
        return opened;
    }
}

using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The page's life: showing a world, stopping the live polling, and leaving the page.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// Shows a world: a new one starts afresh (pick, search and matches cleared, the view where it was last looked at);
    /// the same one again just refreshes. Live worlds poll their players.
    /// </summary>
    public void Show(WorldSession session)
    {
        bool same = _session == session;
        _session = session;
        // A search still running searched the world as it was: dropped when it ends.
        _searches++;
        if (_searching)
        {
            _searching = false;
            SearchInfo.Text = Lang.T("map.msg.searchStale");
        }
        if (!same)
        {
            Map.Show(session);
            Spot = null;
            Target = null;
            Map.Chosen = null;
            _pickBox.IsVisible = false;
            _hits = [];
            Hits.ItemsSource = null;
            SearchInfo.Text = "";
            Map.Pins = [];
            Map.PinChosen = -1;
            Matches = [];
            Map.Matches = Matches;
            _filtered = false;
            ZoneInfo.Text = "";
            // Where the map was last looked at (remembered), or the whole world.
            if (StartView() is var (vx, vz, vm))
            {
                Map.LookAt(vx, vz, vm);
            }
            else
            {
                Map.LookAt(0, 0, session.World.Rules.TotalRadius * 2 / 900f);
            }
            RMaxBox.PlaceholderText = session.World.Rules.TotalRadius.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            Map.Refresh();
        }
        // Saved, discarded, reloaded or edited since: the zone counts are read again when next asked.
        _stats = null;
        _statsGeneration++;
        var w = session.World;
        Meta.Text = session.IsLive
            ? Lang.T("map.meta.live", w.Name, session.Label, w.SeedName, w.ObjectCount)
            : Lang.T("map.meta.file", w.Name, w.SeedName, w.SaveNumber, w.ObjectCount, w.ChunkCount);
        _liveBox.IsVisible = session.IsLive;
        SaveButton.Content = session.IsLive ? Lang.T("save.label.apply") : Lang.T("map.label.save");
        SaveButton.Tip(session.IsLive ? "map.apply" : "map.save");
        UpdatePending();
        FillEdited();
        _players.Stop();
        Map.Players = [];
        PlaceLabels();
        if (session.IsLive)
        {
            _ = PollPlayers();
            _players.Start();
        }
    }

    /// <summary>
    /// Stops polling the live players (the page is left).
    /// </summary>
    public void Stop() => _players.Stop();

    /// <summary>
    /// The world shown was closed: its players are no longer asked for (its connection is gone), and the next world
    /// shown starts afresh.
    /// </summary>
    public void Forget()
    {
        Stop();
        _session = null;
        Map.Players = [];
        PlaceLabels();
    }

    /// <summary>
    /// Leaving the map for good (the window closed): what runs is stopped.
    /// </summary>
    public void Dispose()
    {
        Stop();
        Map.Dispose();
    }
}

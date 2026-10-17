using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The zone counts, and the matches of the zone filter shown on the map.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// Every zone's counts, read once (in the background) until the world changes; null when no world is shown, or when
    /// a world was shown again while they were read (another world, or the same one saved or discarded: they are from
    /// before, dropped).
    /// </summary>
    private async Task<int[]?> Stats()
    {
        if (_session is not { } s)
        {
            return null;
        }
        if (_stats == null)
        {
            int generation = _statsGeneration;
            ZoneInfo.Text = Lang.T("map.msg.reading");
            _reading++;
            int[] stats;
            try
            {
                stats = await Task.Run(() => ZoneStats.Compute(s.World, s.Edits, (x, z) => (int)s.Terrain.BiomeAt(x * 64f, z * 64f)));
            }
            finally
            {
                _reading--;
            }
            if (_session != s || _statsGeneration != generation)
            {
                // The status said this read was going on: cleared, unless another read is, or has shown its counts.
                if (_reading == 0 && _stats == null)
                {
                    ZoneInfo.Text = "";
                }
                return null;
            }
            _stats = stats;
        }
        return _stats;
    }

    /// <summary>
    /// The filter changed: matches shown are worked out again.
    /// </summary>
    private void Refilter()
    {
        if (_stats != null && _filtered)
        {
            ApplyFilter();
        }
    }

    /// <summary>
    /// Works out the matching zones and shows them on the map with their count.
    /// </summary>
    private void ApplyFilter()
    {
        if (_stats is not { } stats || _session is not { } s)
        {
            return;
        }
        _filtered = true;
        if (RadiusProblem() is string problem)
        {
            // Nothing matches a filter that cannot be read: said, not taken as no limit.
            Matches = [];
            Map.Matches = [];
            ZoneInfo.Text = problem;
            Map.RequestNextFrameRendering();
            return;
        }
        var (zones, objects) = Filter().Match(stats);
        Matches = zones;
        Map.Matches = zones;
        int marked = s.Edits.ResetCount;
        int all = stats.Length / ZoneStats.Stride;
        ZoneInfo.Text = marked > 0 ? Lang.T("map.msg.matchMarked", zones.Count, all, objects, marked) : Lang.T("map.msg.match", zones.Count, all, objects);
        Map.RequestNextFrameRendering();
    }

    /// <summary>
    /// Show matching: reads the zones if needed, then shows the matches.
    /// </summary>
    internal async Task ShowMatching()
    {
        if (await Stats() != null)
        {
            ApplyFilter();
        }
    }
}

using Avalonia.Controls;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The map page's zones across the world: picked by the zone filter (biomes, buildings and ground edits near them,
/// distance from the centre), shown on the map and marked for reset.
/// </summary>
public sealed partial class MapPage
{
    // ---- Zones across the world, picked by filter, to reset.
    /// <summary>
    /// The zone filter as the boxes set it.
    /// </summary>
    private ZoneFilter Filter() =>
        new(BiomeButtons.Where(b => b.Value.IsChecked == true).Select(b => b.Key).ToHashSet(), NoBuildBox.IsChecked == true, (int)(DistBox.Value ?? 0),
            NoEditBox.IsChecked == true, OnlyGenBox.IsChecked == true, Radius(RMinBox).Value, Radius(RMaxBox).Value);

    /// <summary>
    /// A distance box's metres: blank is no limit (null). A point or the computer's own decimal mark, spaces between
    /// thousands; Bad when it is not a finite number (it was taken as no limit without a word).
    /// </summary>
    private static (float? Value, bool Bad) Radius(TextBox b)
    {
        string t = string.Concat((b.Text ?? "").Where(c => !char.IsWhiteSpace(c)));
        if (t.Length == 0)
        {
            return (null, false);
        }
        const System.Globalization.NumberStyles F = System.Globalization.NumberStyles.Float;
        return (float.TryParse(t, F, System.Globalization.CultureInfo.InvariantCulture, out float v) || float.TryParse(t, F, System.Globalization.CultureInfo.CurrentCulture, out v))
            && float.IsFinite(v) ? (v, false) : (null, true);
    }

    /// <summary>What is wrong with the distance boxes, null when nothing.</summary>
    private string? RadiusProblem() => Radius(RMinBox).Bad || Radius(RMaxBox).Bad
        ? Lang.T("map.error.radius")
        : null;

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

    /// <summary>
    /// Mark for reset: after asking, marks every matching zone for reset (with its ground edits when the box says so).
    /// </summary>
    internal async Task MarkMatching()
    {
        if (_session is not { } s || await Stats() == null)
        {
            return;
        }
        ApplyFilter();
        if (Matches.Count == 0)
        {
            await Tell(RadiusProblem() ?? Lang.T("map.msg.noMatch"));
            return;
        }
        bool live = s.IsLive, ground = GroundBox.IsChecked == true;
        int generation = _statsGeneration;
        string question = string.Join("\n",
            Lang.T("map.ask.mark.text", Matches.Count) + "\n",
            ground ? Lang.T("map.ask.mark.removedGround") : Lang.T("map.ask.mark.removed"),
            live ? Lang.T("map.ask.mark.againLive") : Lang.T("map.ask.mark.again"),
            live ? Lang.T("map.ask.mark.apply") : Lang.T("map.ask.mark.save"));
        if (!await Confirm(question))
        {
            return;
        }
        // A world shown while asking (another one, or this one saved, discarded or read again): the matches were
        // worked out before, and are gone.
        if (_session != s || _statsGeneration != generation)
        {
            return;
        }
        foreach (var (x, z) in Matches)
        {
            s.Edits.SetReset(new ZoneReset(x, z, true, ground), true);
        }
        ApplyFilter();
        UpdatePending();
    }

    /// <summary>
    /// Unmark all: drops every zone reset still waiting.
    /// </summary>
    internal async Task UnmarkAll()
    {
        if (_session is not { } s)
        {
            return;
        }
        var resets = s.Edits.Resets;
        if (resets.Count == 0)
        {
            await Tell(Lang.T("map.msg.noneMarked"));
            return;
        }
        foreach (var r in resets)
        {
            s.Edits.SetReset(r, false);
        }
        if (_stats != null)
        {
            ApplyFilter();
        }
        UpdatePending();
    }
}

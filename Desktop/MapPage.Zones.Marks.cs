using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Marking zones for reset, and unmarking them.
/// </summary>
public sealed partial class MapPage
{
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

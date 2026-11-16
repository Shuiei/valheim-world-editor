using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Marking zones for reset, and unmarking them.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// Mark for reset: after asking, marks every matching zone for reset, the players' buildings kept or not and the
    /// ground edits undone or not as the two boxes say (the Region tool's Reset zones has the same two).
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
        bool live = s.IsLive, keep = KeepBox.IsChecked == true, ground = GroundBox.IsChecked == true;
        int generation = _statsGeneration;
        // The same lines as the Region tool's question: what goes, the buildings kept or not, the ground.
        string question = string.Join("\n", new[]
        {
            Lang.T("map.ask.mark.text", Matches.Count) + "\n",
            keep ? Lang.T("reset.ask.removedKeep") : Lang.T("reset.ask.removedAll"),
            ground ? Lang.T("reset.ask.ground") : null,
            live ? Lang.T("reset.ask.againLive") : Lang.T("reset.ask.again"),
            live ? Lang.T("map.ask.mark.apply") : Lang.T("map.ask.mark.save"),
        }.OfType<string>());
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
            s.Edits.SetReset(new ZoneReset(x, z, keep, ground), true);
        }
        ApplyFilter();
        UpdatePending();
    }

    /// <summary>
    /// Unmark matching: drops the reset of the matching zones that are marked, the others' left waiting.
    /// </summary>
    internal async Task UnmarkMatching()
    {
        if (_session is not { } s || await Stats() == null)
        {
            return;
        }
        ApplyFilter();
        var marked = s.Edits.Resets.ToDictionary(r => (r.X, r.Z));
        var found = Matches.Where(marked.ContainsKey).ToList();
        if (found.Count == 0)
        {
            await Tell(RadiusProblem() ?? Lang.T("map.msg.noMatchMarked"));
            return;
        }
        foreach (var zone in found)
        {
            s.Edits.SetReset(marked[zone], false);
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

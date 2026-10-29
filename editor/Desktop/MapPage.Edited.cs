using Avalonia.Controls;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The edited zones: the list most edited first, the zone picked from it, and the height range of each zone's edits.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>Builds the edited zones' section: its list, which picks the zone from it.</summary>
    /// <returns>The section, its header carrying the count.</returns>
    private Expander BuildEdited()
    {
        EditedList.SelectionChanged += (_, _) =>
        {
            if (EditedList.SelectedIndex is int i and >= 0 && i < _edited.Count)
            {
                var e = _edited[i];
                Map.LookAt(e.ZoneX * 64, e.ZoneZ * 64, MathF.Min(Map.MetersPerPixel, 0.5f));
                Pick(e.ZoneX, e.ZoneZ);
            }
        };
        return Section(Lang.T("map.head.edited"), false, EditedList);
    }

    // ---- The edited zones, most edited first, and the picked zone's edits up close.
    /// <summary>
    /// Lists the zones with ground or paint edits, most edited first, with their range of change; those with changes
    /// pending are marked not saved (live: not applied).
    /// </summary>
    private void FillEdited()
    {
        if (_session is not { } s)
        {
            return;
        }
        _edited = [.. s.Edits.Copies(e => e.HeightCount + e.PaintCount > 0).OrderByDescending(e => e.HeightCount + e.PaintCount)];
        EditedList.ItemsSource = _edited.Select(e =>
        {
            var (lo, hi) = Range(e);
            string key = !e.Changed ? "map.edited.line" : s.IsLive ? "map.edited.unapplied" : "map.edited.unsaved";
            return Lang.T(key, e.ZoneX, e.ZoneZ, e.HeightCount, e.PaintCount, lo, hi);
        }).ToList();
        ((TextBlock)_editedSection.Header!).Text = Lang.T("map.head.editedCount", _edited.Count);
    }

    /// <summary>
    /// The lowest and highest height change in a zone's edited points, in metres; 0, 0 when none.
    /// </summary>
    private static (float Lo, float Hi) Range(ZoneEdit e)
    {
        float lo = 0, hi = 0;
        bool any = false;
        for (int i = 0; i < EditStore.Cells; i++)
        {
            if (e.Modified[i])
            {
                float d = e.Level[i] + e.Smooth[i];
                lo = any ? MathF.Min(lo, d) : d;
                hi = any ? MathF.Max(hi, d) : d;
                any = true;
            }
        }
        return (lo, hi);
    }

    // Zone detail and the edited zones.
    /// <summary>
    /// The edited zones, most edited first; picking one goes there.
    /// </summary>
    internal ListBox EditedList { get; } = new() { MaxHeight = 200, FontSize = 11 };
    /// <summary>
    /// The edited zones' section, its header carrying their count.
    /// </summary>
    private readonly Expander _editedSection;
    /// <summary>
    /// The zones listed in <c>EditedList</c>, in its order.
    /// </summary>
    private List<ZoneEdit> _edited = [];
}

using Avalonia.Controls;
using TerrainEditor.App;

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
}

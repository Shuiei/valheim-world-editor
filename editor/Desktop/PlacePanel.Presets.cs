using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The presets: the built-in and your own, the list of them, and saving or deleting one.
public sealed partial class PlacePanel
{
    /// <summary>The presets: "None", the built-in ones, then your own.</summary>
    internal ComboBox PresetBox { get; }

    /// <summary>Saves the chosen kinds, weights and scatter settings as your own preset.</summary>
    internal Button SavePresetButton { get; } = new() { Content = Lang.T("place.label.savePreset"), FontSize = 12 };

    /// <summary>Deletes the selected preset (your own only).</summary>
    internal Button DeletePresetButton { get; } = new() { Content = Lang.T("common.delete"), FontSize = 12, IsEnabled = false };

    /// <summary>A preset in the list.</summary>
    /// <param name="Preset">The preset.</param>
    /// <param name="Own">One of yours (it can be deleted), not a built-in one.</param>
    private sealed record PresetEntry(PlaceTool.Preset Preset, bool Own);

    /// <summary>The preset at an index of the list, or null for the first entry or past the end.</summary>
    private PresetEntry? PresetAt(int i) => i <= 0 ? null : i <= PlaceTool.BuiltIn.Length ? new(PlaceTool.BuiltIn[i - 1], false)
        : i - 1 - PlaceTool.BuiltIn.Length < Memory.Presets.Count ? new(Memory.Presets[i - 1 - PlaceTool.BuiltIn.Length], true) : null;

    /// <summary>Fills the preset list, selecting your own preset of that name if given.</summary>
    private void FillPresets(string? select = null)
    {
        _filling = true;
        PresetBox.ItemsSource = new[] { Lang.T("place.preset.none") }.Concat(PlaceTool.BuiltIn.Select(PlaceTool.ShownName)).Concat(Memory.Presets.Select(p => Lang.T("place.preset.yours", p.Name))).ToList();
        int own = Memory.Presets.FindIndex(p => p.Name == select);
        PresetBox.SelectedIndex = own >= 0 ? 1 + PlaceTool.BuiltIn.Length + own : 0;
        DeletePresetButton.IsEnabled = own >= 0;
        _filling = false;
    }

    /// <summary>Remembers the chosen kinds and weights for the next time (saves the memory a moment later).</summary>
    private void Remember()
    {
        Memory.Chosen = [.. T.Chosen];
        Memory.Weights = new(T.Weights);
        Memory.SaveLater();
    }
}

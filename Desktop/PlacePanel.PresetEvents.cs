using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Choosing, saving and deleting presets from the panel.
public sealed partial class PlacePanel
{
    /// <summary>The preset list and the save and delete of presets.</summary>
    private void WirePresets(PlaceTool t)
    {
        PresetBox.SelectionChanged += (_, _) =>
        {
            DeletePresetButton.IsEnabled = PresetAt(PresetBox.SelectedIndex) is { Own: true };
            if (_filling)
            {
                return;
            }
            if (PresetAt(PresetBox.SelectedIndex) is not { } chosen)
            {
                // None, after a preset: its kinds are unticked.
                if (PresetBox.SelectedIndex == 0 && T.Chosen.Count > 0)
                {
                    T.Chosen.Clear();
                    Chosen();
                }
                return;
            }
            var known = Creatable().Select(c => c.Name).ToHashSet();
            Say(t.Load(chosen.Preset, known.Contains));
            Remember();
            Fill();
            FillList();
        };
        SavePresetButton.Tip("place.savePreset");
        SavePresetButton.Click += async (_, _) =>
        {
            if (t.Chosen.Count == 0)
            {
                Say(Said.Of("place.msg.tickFirst"));
                return;
            }
            string? name = (await AskName())?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            var p = new PlaceTool.Preset(name, t.Chosen.ToDictionary(n => n, t.WeightOf), t.Density, t.Spacing, t.SizeMin, t.SizeMax, t.Tilt, t.Clump, t.Patch);
            Memory.Presets = [.. Memory.Presets.Where(x => x.Name != name).Append(p).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
            Memory.Save();
            FillPresets(name);
            Say(Said.Of("place.msg.presetSaved", name));
        };
        DeletePresetButton.Click += async (_, _) =>
        {
            if (PresetAt(PresetBox.SelectedIndex) is not { Own: true } p || !await Confirm(Lang.T("place.ask.deletePreset.text", p.Preset.Name)))
            {
                return;
            }
            Memory.Presets.RemoveAll(x => x.Name == p.Preset.Name);
            Memory.Save();
            FillPresets();
        };
    }
}

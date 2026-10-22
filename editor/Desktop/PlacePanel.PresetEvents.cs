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
                // None, after a preset: its kinds are unticked (while a preset shows, the kinds chosen are its own:
                // changing them shows None, and one that loads nothing is not shown).
                if (PresetBox.SelectedIndex == 0 && T.Chosen.Count > 0)
                {
                    T.Chosen.Clear();
                    Chosen();
                }
                return;
            }
            LoadPreset(t, chosen.Preset);
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

    /// <summary>
    /// Loads the preset chosen in the list. When the world can make none of its kinds nothing is loaded and the list says
    /// None again: choosing None next would untick the kinds chosen before it.
    /// </summary>
    /// <param name="t">The Place tool.</param>
    /// <param name="preset">The preset chosen.</param>
    private void LoadPreset(PlaceTool t, PlaceTool.Preset preset)
    {
        var known = Creatable().Select(c => c.Name).ToHashSet();
        Say(t.Load(preset, known.Contains));
        if (!preset.Kinds.Keys.Any(known.Contains))
        {
            _filling = true;
            PresetBox.SelectedIndex = 0;
            _filling = false;
            return;
        }
        Remember();
        Fill();
        FillList();
    }
}

using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// The kinds the world can make, for the "with" list: worked out again for another world (its templates are its
    /// own), when a live merge brings it new kinds, or when the game's data, the mods' manifests or their catalogue
    /// change, keeping the chosen kind when it is still offered.
    /// </summary>
    private void FillCreatable()
    {
        if (_view.Scene?.World is { } w && _creatableFrom != (w, w.TemplatesGeneration, GameData.Generation, ModManifests.Generation, ModCatalogue.Generation))
        {
            _creatableFrom = (w, w.TemplatesGeneration, GameData.Generation, ModManifests.Generation, ModCatalogue.Generation);
            int chosen = ToBox.SelectedIndex >= 0 && ToBox.SelectedIndex < _creatable.Count ? _creatable[ToBox.SelectedIndex] : 0;
            _creatable = [.. w.Creatable.Where(p => _nameOf(p) != null).OrderBy(p => _nameOf(p), StringComparer.OrdinalIgnoreCase)];
            ToBox.ItemsSource = _creatable.Select(p => _nameOf(p)!).ToList();
            ToBox.SelectedIndex = _creatable.IndexOf(chosen);
        }
        else if (_view.Scene?.World == null && _creatableFrom.World != null)
        {
            // No world open: the last one is not kept alive by the list.
            _creatableFrom = default;
            _creatable = [];
            ToBox.ItemsSource = Array.Empty<string>();
        }
    }
}

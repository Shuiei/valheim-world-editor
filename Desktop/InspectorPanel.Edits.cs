using System.Globalization;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>The changes made: values set (null: removed), and the contents when they changed.</summary>
    internal (List<FieldChange> Set, List<ItemUpload>? Inventory) Changes()
    {
        var set = new List<FieldChange>();
        foreach (var f in FieldList)
        {
            if (f.Removed)
            {
                set.Add(new FieldChange(f.Section, f.Key.ToString(CultureInfo.InvariantCulture), null));
            }
            else if (f.Now != f.Value && f.Section != "bytes")
            {
                set.Add(new FieldChange(f.Section, f.Key.ToString(CultureInfo.InvariantCulture), f.Now));
            }
        }
        // A value a mod's manifest shows only is never set, by name or by number.
        foreach (var (section, key, value) in Added.Where(a => !ReadOnlyKey(a.Key)))
        {
            set.Add(new FieldChange(section, key, value));
        }
        var inv = Items != null && _inventoryError == null && !ItemsReadOnly && ItemsKey() != _itemsAtOpen ? Items.Select(i => i.Upload()).ToList() : null;
        _standError = null;
        if (Stand != null && _z != null && StandKey() != _standAtOpen)
        {
            try
            {
                var held = Stand.Where(r => r.Name != "").Select(r => new StandData.Held(r.Name, r.Quality, r.Variant)).ToList();
                set.AddRange(_armourStand ? StandData.ForArmourStand(_z.Prefab, held, StandWay) : StandData.ForItemStand(_z.Prefab, held.FirstOrDefault(), StandWay));
            }
            catch (StandRefusalException ex)
            {
                Log.CaughtBrief(ex, "setting what a stand holds");
                _standError = ex.Reason;
            }
            catch (ArgumentException ex)
            {
                Log.CaughtBrief(ex, "setting what a stand holds");
                _standError = Said.Raw(ex.Message);
            }
        }
        return (set, inv);
    }

    /// <summary>Enables Apply and Revert when something changed.</summary>
    private void Dirty()
    {
        var (set, inv) = Changes();
        ApplyButton.IsEnabled = RevertButton.IsEnabled = set.Count > 0 || inv != null;
    }
}

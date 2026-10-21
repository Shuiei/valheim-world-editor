using Avalonia.Controls;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// Puts the name typed or picked in the picked slot's box into the slot: a new item when the slot is empty, or
    /// the existing item renamed. Nothing changes when the name is not a known item or is already the slot's.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="pick">The box the name is read from.</param>
    /// <param name="sx">The slot's column.</param>
    /// <param name="sy">The slot's row.</param>
    private void ChooseSlotItem(List<ItemRow> items, AutoCompleteBox pick, int sx, int sy)
    {
        string name = pick.Text?.Trim() ?? "";
        string? known = PrefabCatalog.Items.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        // What the slot holds now: a pick and the box then losing focus (once the grid is drawn
        // again) both come here, and must not put the item in twice.
        var now = items.FirstOrDefault(i => i.X == sx && i.Y == sy);
        if (known == null || known == now?.Name)
        {
            return;
        }
        if (now == null)
        {
            items.Add(new ItemRow { Name = known, X = sx, Y = sy });
        }
        else
        {
            now.Name = known;
        }
        Dirty();
        RenderItems();
    }
}

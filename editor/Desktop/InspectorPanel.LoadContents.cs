using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// Reads the object's contents for the panel: its slot grid and its items, or the reason they cannot be read.
    /// </summary>
    /// <param name="z">The object's data.</param>
    private void LoadContents(ZdoData z)
    {
        var details = PrefabCatalog.Details(z.Prefab);
        byte[]? items = z.GetBytes(ObjectData.ItemsKey);
        Items = null;
        _inventoryError = null;
        _grid = (details?.ContainerW ?? 0, details?.ContainerH ?? 0);
        if (items != null || details?.ContainerW > 0)
        {
            try
            {
                var inv = items != null ? InventoryData.Read(items) : new InventoryData();
                Items = [.. inv.Items.Select(i => new ItemRow
                {
                    Name = PrefabCatalog.NameOf(i.Prefab) ?? i.Prefab.ToString(CultureInfo.InvariantCulture),
                    Stack = i.Stack,
                    Quality = i.Quality,
                    Durability = i.Durability,
                    X = i.X,
                    Y = i.Y,
                    Variant = i.Variant,
                    WorldLevel = i.WorldLevel,
                    CrafterId = i.CrafterId.ToString(CultureInfo.InvariantCulture),
                    CrafterName = i.CrafterName,
                    Equipped = i.Equipped,
                    PickedUp = i.PickedUp,
                    Cheated = i.Cheated,
                    CustomData = new(i.CustomData),
                })];
            }
            catch (Exception ex) when (CheckedReads.Damaged(ex))
            {
                Log.Caught(ex, "reading a container's items to inspect them");
                _inventoryError = ex.Message;
                Items = [];
            }
        }
    }
}

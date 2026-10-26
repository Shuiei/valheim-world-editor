using System.Globalization;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// Building a container's inventory and applying an inspector edit to an object's saved values.
/// </summary>
public static partial class ObjectData
{
    /// <summary>
    /// A container's contents from a list of items, each value clamped to what the save holds (stack and
    /// quality 1..65535, slot and world level 0..255).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An entry is not an item of the game: unknown, or a prefab that is not an item (a creature, a piece), which the
    /// game would drop from the container on load.
    /// </exception>
    /// <param name="list">The items.</param>
    /// <param name="notes">Told "Wood: stack of 70 cut to 50 (the item's maximum)" for each stack cut, when given.</param>
    public static InventoryData BuildInventory(List<ItemUpload> list, ICollection<string>? notes = null)
    {
        InventoryData inv = new();
        foreach (ItemUpload u in list)
        {
            int prefab = u.Prefab ?? StableHash.Of(u.Name ?? "");
            if (PrefabCatalog.ItemKindOf(prefab) is not { } kind)
            {
                throw new ArgumentException($"“{u.Name ?? prefab.ToString(CultureInfo.InvariantCulture)}” is not an item of the game.");
            }
            // The game keeps a stack's worth: a larger stack is cut to the item's largest (1: the item does not
            // stack). The catalogue always knows it: each build of the editor scans the game again.
            int stack = Math.Clamp(u.Stack, 1, 65535);
            if (stack > kind.MaxStack)
            {
                notes?.Add($"{u.Name ?? PrefabCatalog.NameOf(prefab) ?? prefab.ToString(CultureInfo.InvariantCulture)}: stack of {stack} cut to {kind.MaxStack} (the item's maximum)");
                stack = kind.MaxStack;
            }
            inv.Items.Add(new InventoryData.Item
            {
                Prefab = prefab,
                Stack = stack,
                Quality = Math.Clamp(u.Quality, 1, 65535),
                Durability = u.Durability,
                X = Math.Clamp(u.X, 0, 255),
                Y = Math.Clamp(u.Y, 0, 255),
                Variant = u.Variant,
                WorldLevel = Math.Clamp(u.WorldLevel, 0, 255),
                CrafterId = long.TryParse(u.CrafterId, NumberStyles.Integer, CultureInfo.InvariantCulture, out long c) ? c : 0,
                CrafterName = u.CrafterName ?? "",
                Equipped = u.Equipped,
                PickedUp = u.PickedUp,
                Cheated = u.Cheated,
                CustomData = u.CustomData ?? [],
            });
        }
        return inv;
    }

    /// <summary>
    /// The object with these values set (null removes one) and these contents: throws FormatException,
    /// ArgumentException or OverflowException for values that do not fit.
    /// </summary>
    /// <param name="bytes">The object's saved bytes.</param>
    /// <param name="set">Values to set or remove, if any.</param>
    /// <param name="inventory">
    /// The container's new contents (also marked as having had its default loot); null leaves them as they are.
    /// </param>
    /// <param name="notes">Told of stacks cut to the item's maximum (BuildInventory), when given.</param>
    public static ZdoData Edited(byte[] bytes, List<FieldChange>? set, List<ItemUpload>? inventory, ICollection<string>? notes = null)
    {
        var z = ZdoData.Parse(bytes);
        foreach (FieldChange f in set ?? [])
        {
            z.Set(f.Section, ZdoKeys.Parse(f.Key), f.Value);
        }
        if (inventory != null)
        {
            z.SetBytes(ItemsKey, BuildInventory(inventory, notes).Write());
            // Told it has its default items already: a chest never opened fills itself with the game's
            // loot when it first loads (Container.Awake), over what was set here.
            z.Set("ints", AddedDefaultItemsKey, "1");
        }
        return z;
    }
}

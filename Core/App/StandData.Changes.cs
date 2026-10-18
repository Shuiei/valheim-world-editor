using System.Text;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

public static partial class StandData
{
    /// <summary>
    /// The values that make an item stand hold this item (null: nothing), hanging this way (the game's
    /// orientations: 0 is the first). Throws <see cref="StandRefusalException"/> when the stand does not take the item.
    /// Quality is clamped to 1..10, the variant to the item's.
    /// </summary>
    public static List<FieldChange> ForItemStand(int stand, Held? held, int orientation)
    {
        if (held == null)
        {
            // As ItemStand.DropItem leaves it.
            return [new("ints", "item", "0"), new("ints", "type", "0"), new("bytes", "itemData", null)];
        }
        if (ItemStandRefusal(stand, held.Item) is { } why)
        {
            throw new StandRefusalException(why);
        }
        int hash = StableHash.Of(held.Item);
        var kind = PrefabCatalog.ItemKindOf(hash)!;
        int quality = Math.Clamp(held.Quality, 1, 10), variant = Variant(kind, held.Variant);
        return
        [
            new("ints", "item", I(hash)), new("ints", "variant", I(variant)), new("ints", "quality", I(quality)),
            new("ints", "type", I(Math.Max(0, orientation))), new("bytes", "itemData", ItemData(hash, kind, quality, variant)),
        ];
    }

    /// <summary>
    /// The values that make an armour stand wear these items (each in the first free slot that takes it;
    /// the other slots emptied), in this pose when given. Throws <see cref="StandRefusalException"/> when one fits nowhere.
    /// </summary>
    public static List<FieldChange> ForArmourStand(int stand, IReadOnlyList<Held> held, int? pose)
    {
        Said standName = StandName(stand);
        int[][] slots = PrefabCatalog.ArmourSlotsOf(stand) ?? throw new StandRefusalException(Said.Of("stand.msg.notArmourStand", standName));
        var worn = new (Held Item, int Hash, PrefabCatalog.ItemKind Kind)?[slots.Length];
        foreach (Held h in held)
        {
            int hash = StableHash.Of(h.Item);
            var kind = PrefabCatalog.ItemKindOf(hash) ?? throw new StandRefusalException(Said.Of("stand.msg.notAnItem", h.Item));
            int slot = Enumerable.Range(0, slots.Length).FirstOrDefault(i => worn[i] == null && FitsArmourSlot(slots[i], kind), -1);
            if (slot < 0)
            {
                throw new StandRefusalException(Enumerable.Range(0, slots.Length).Any(i => FitsArmourSlot(slots[i], kind))
                    ? Said.Of("stand.msg.noFreeSlot", standName, h.Item, TypeWords(kind.Type))
                    : Said.Of("stand.msg.cannotWear", standName, h.Item, TypeWords(kind.Type)));
            }
            worn[slot] = (h, hash, kind);
        }
        var set = new List<FieldChange>();
        for (int i = 0; i < slots.Length; i++)
        {
            string p = I(i);
            if (worn[i] is var (h, hash, kind))
            {
                int quality = Math.Clamp(h.Quality, 1, 10), variant = Variant(kind, h.Variant);
                set.Add(new("ints", p + "_item", I(hash)));
                set.Add(new("ints", p + "_variant", I(variant)));
                set.Add(new("bytes", p + "_itemData", ItemData(hash, kind, quality, variant)));
            }
            else
            {
                set.Add(new("ints", p + "_item", "0"));
                set.Add(new("ints", p + "_variant", null));
                set.Add(new("bytes", p + "_itemData", null));
            }
        }
        if (pose is int ps)
        {
            set.Add(new("ints", "pose", I(Math.Max(0, ps))));
        }
        return set;
    }

    /// <summary>The variant clamped to the item's variants; 0 for an item without any.</summary>
    private static int Variant(PrefabCatalog.ItemKind kind, int variant) => kind.Variants > 0 ? Math.Clamp(variant, 0, kind.Variants - 1) : 0;

    /// <summary>
    /// ItemDrop.SaveToZDO's bytes for a new item: the item format version, then the item (full durability for
    /// its quality). Base64, as a "bytes" value is written.
    /// </summary>
    private static string ItemData(int hash, PrefabCatalog.ItemKind kind, int quality, int variant)
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write((byte)InventoryData.Version);
            InventoryData.WriteItem(w, new InventoryData.Item { Prefab = hash, Quality = quality, Variant = variant, Durability = kind.NewDurability(quality) });
        }
        return Convert.ToBase64String(ms.ToArray());
    }
}

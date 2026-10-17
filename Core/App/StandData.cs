using System.Globalization;
using System.Text;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// What item stands and armour stands hold, as the game saves it. An item stand (ItemStand: itemstand,
/// itemstandh, the boss altars) keeps the item's prefab hash in "item", with its "variant", its
/// "quality" and the way it hangs in "type"; an armour stand (ArmorStand) keeps slot i's item in
/// "&lt;i&gt;_item" and "&lt;i&gt;_variant", and the stand's "pose". Both keep the whole item as
/// ItemDrop.SaveToZDO writes it ("itemData", "&lt;i&gt;_itemData": the item format version in one byte, then
/// the item as an inventory keeps it): that is what a player gets back on taking it.
/// </summary>
public static class StandData
{
    /// <summary>An item on a stand.</summary>
    /// <param name="Item">Its prefab name.</param>
    /// <param name="Quality">Its quality (upgrade level).</param>
    /// <param name="Variant">Its variant (a shield's style).</param>
    public sealed record Held(string Item, int Quality = 1, int Variant = 0);

    /// <summary>Whether the prefab is an item stand (the catalogue has its rules).</summary>
    public static bool IsItemStand(int prefab) => PrefabCatalog.ItemStandOf(prefab) != null;

    /// <summary>Whether the prefab is an armour stand (the catalogue has its slots).</summary>
    public static bool IsArmourStand(int prefab) => PrefabCatalog.ArmourSlotsOf(prefab) != null;

    /// <summary>ItemDrop.ItemData.ItemType, as words, in English (what the refusals and Claude are told).</summary>
    /// <param name="type">The item type's number.</param>
    public static string TypeName(int type) => TypeWords(type).English;

    /// <summary>ItemDrop.ItemData.ItemType, as words in both languages (the inspector shows the user's).</summary>
    /// <param name="type">The item type's number.</param>
    public static Said TypeWords(int type) => type switch
    {
        1 => Said.Of("inspect.type.material"),
        2 => Said.Of("inspect.type.foodOrPotion"),
        3 => Said.Of("inspect.type.oneHandedWeapon"),
        4 => Said.Of("inspect.type.bow"),
        5 => Said.Of("inspect.type.shield"),
        6 => Said.Of("inspect.type.helmet"),
        7 => Said.Of("inspect.type.chestArmour"),
        9 or 23 => Said.Of("inspect.type.ammo"),
        10 => Said.Of("inspect.type.customization"),
        11 => Said.Of("inspect.type.legsArmour"),
        12 => Said.Of("inspect.type.hands"),
        13 => Said.Of("inspect.type.trophy"),
        14 or 22 => Said.Of("inspect.type.twoHandedWeapon"),
        15 => Said.Of("inspect.type.torch"),
        16 => Said.Of("inspect.type.miscItem"),
        17 => Said.Of("inspect.type.cape"),
        18 => Said.Of("inspect.type.utilityItem"),
        19 => Said.Of("inspect.type.tool"),
        20 => Said.Of("inspect.type.atgeir"),
        21 => Said.Of("inspect.type.fish"),
        24 => Said.Of("inspect.type.trinket"),
        _ => Said.Of("inspect.type.item"),
    };

    /// <summary>Why this item stand does not take the item (ItemStand.CanAttach), or null when it does.</summary>
    /// <param name="stand">The stand's prefab hash.</param>
    /// <param name="item">The item's prefab name.</param>
    public static string? ItemStandRefusal(int stand, string item)
    {
        string standName = PrefabCatalog.DisplayName(stand) ?? "This object";
        if (PrefabCatalog.ItemStandOf(stand) is not { } rule)
        {
            return $"{standName} is not an item stand.";
        }
        if (PrefabCatalog.ItemKindOf(StableHash.Of(item)) is not { } kind)
        {
            return $"“{item}” is not an item of the game.";
        }
        if (!kind.Attach)
        {
            return $"{item} cannot hang on a stand (the game shows it on none).";
        }
        if (rule.Refuses.Contains(item))
        {
            return $"{standName} refuses {item}.";
        }
        if (rule.Takes.Contains(item) || rule.Types.Contains(kind.Type))
        {
            return null;
        }
        return $"{standName} does not take {item} (a {TypeName(kind.Type)}); it takes: {string.Join(", ", rule.Types.Select(TypeName).Distinct())}.";
    }

    /// <summary>
    /// Whether an armour stand's slot takes the item (ArmorStand.CanAttach, and its check that anything
    /// but chest and legs pieces has an attach point).
    /// </summary>
    /// <param name="slotTypes">The item types the slot takes; empty: any.</param>
    /// <param name="kind">The item's kind.</param>
    public static bool FitsArmourSlot(int[] slotTypes, PrefabCatalog.ItemKind kind)
    {
        int type = kind.AttachOverride != 0 ? kind.AttachOverride : kind.Type;
        if (slotTypes.Length > 0 && !slotTypes.Contains(type))
        {
            return false;
        }
        return kind.Type is 7 or 11 || kind.Attach || kind.AttachSkin;
    }

    /// <summary>
    /// The values that make an item stand hold this item (null: nothing), hanging this way (the game's
    /// orientations: 0 is the first). Throws ArgumentException when the stand does not take the item.
    /// Quality is clamped to 1..10, the variant to the item's.
    /// </summary>
    public static List<FieldChange> ForItemStand(int stand, Held? held, int orientation)
    {
        if (held == null)
        {
            // As ItemStand.DropItem leaves it.
            return [new("ints", "item", "0"), new("ints", "type", "0"), new("bytes", "itemData", null)];
        }
        if (ItemStandRefusal(stand, held.Item) is string why)
        {
            throw new ArgumentException(why);
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
    /// the other slots emptied), in this pose when given. Throws ArgumentException when one fits nowhere.
    /// </summary>
    public static List<FieldChange> ForArmourStand(int stand, IReadOnlyList<Held> held, int? pose)
    {
        string standName = PrefabCatalog.DisplayName(stand) ?? "This object";
        int[][] slots = PrefabCatalog.ArmourSlotsOf(stand) ?? throw new ArgumentException($"{standName} is not an armour stand.");
        var worn = new (Held Item, int Hash, PrefabCatalog.ItemKind Kind)?[slots.Length];
        foreach (Held h in held)
        {
            int hash = StableHash.Of(h.Item);
            var kind = PrefabCatalog.ItemKindOf(hash) ?? throw new ArgumentException($"“{h.Item}” is not an item of the game.");
            int slot = Enumerable.Range(0, slots.Length).FirstOrDefault(i => worn[i] == null && FitsArmourSlot(slots[i], kind), -1);
            if (slot < 0)
            {
                throw new ArgumentException(Enumerable.Range(0, slots.Length).Any(i => FitsArmourSlot(slots[i], kind))
                    ? $"{standName} has no free slot left for {h.Item} (a {TypeName(kind.Type)})."
                    : $"{standName} cannot wear {h.Item} (a {TypeName(kind.Type)}): it takes armour, capes, belts, shields, weapons and tools.");
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

    /// <summary>What an item stand holds (null: nothing) and the way it hangs.</summary>
    public static (Held? Item, int Orientation) ReadItemStand(ZdoData z)
    {
        int hash = Int(z, "item");
        return (hash == 0 ? null : HeldOf(hash, Int(z, "quality", 1), Int(z, "variant"), z.GetBytes(StableHash.Of("itemData"))), Int(z, "type"));
    }

    /// <summary>What each slot of an armour stand holds (null: nothing), and its pose.</summary>
    /// <param name="z">The stand's saved data.</param>
    /// <param name="slots">How many slots the stand has.</param>
    public static (Held?[] Slots, int Pose) ReadArmourStand(ZdoData z, int slots)
    {
        var held = new Held?[slots];
        for (int i = 0; i < slots; i++)
        {
            string p = I(i);
            int hash = Int(z, p + "_item");
            held[i] = hash == 0 ? null : HeldOf(hash, 1, Int(z, p + "_variant"), z.GetBytes(StableHash.Of(p + "_itemData")));
        }
        return (held, Int(z, "pose"));
    }

    /// <summary>
    /// The held item, its quality and variant from the saved item data when it can be read, else the ones
    /// given. An unknown prefab keeps its hash as name.
    /// </summary>
    private static Held HeldOf(int hash, int quality, int variant, byte[]? itemData)
    {
        if (itemData is { Length: > 2 })
        {
            try
            {
                using BinaryReader r = new(new MemoryStream(itemData), Encoding.UTF8);
                var it = InventoryData.ReadItem(r, r.ReadByte());
                quality = it.Quality;
                variant = it.Variant;
            }
            catch (Exception ex) when (CheckedReads.Damaged(ex))
            {
                // Kept as the stand's own values say.
            }
        }
        return new Held(PrefabCatalog.NameOf(hash) ?? I(hash), quality, variant);
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

    /// <summary>An int value by key name, or <paramref name="otherwise"/> when the object has none.</summary>
    private static int Int(ZdoData z, string key, int otherwise = 0) => z.GetInt(StableHash.Of(key)) ?? otherwise;

    /// <summary>A number as invariant text.</summary>
    private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
}

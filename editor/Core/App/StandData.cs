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
public static partial class StandData
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

    /// <summary>
    /// ItemDrop.ItemData.ItemType, as words in both languages: the refusals name them (Claude reads their English), the
    /// inspector shows the user's.
    /// </summary>
    /// <param name="type">The item type's number.</param>
    public static Said TypeWords(int type) => type switch
    {
        1 => Said.Of("stand.type.material"),
        2 => Said.Of("stand.type.foodOrPotion"),
        3 => Said.Of("stand.type.oneHandedWeapon"),
        4 => Said.Of("stand.type.bow"),
        5 => Said.Of("stand.type.shield"),
        6 => Said.Of("stand.type.helmet"),
        7 => Said.Of("stand.type.chestArmour"),
        9 or 23 => Said.Of("stand.type.ammo"),
        10 => Said.Of("stand.type.customization"),
        11 => Said.Of("stand.type.legsArmour"),
        12 => Said.Of("stand.type.hands"),
        13 => Said.Of("stand.type.trophy"),
        14 or 22 => Said.Of("stand.type.twoHandedWeapon"),
        15 => Said.Of("stand.type.torch"),
        16 => Said.Of("stand.type.miscItem"),
        17 => Said.Of("stand.type.cape"),
        18 => Said.Of("stand.type.utilityItem"),
        19 => Said.Of("stand.type.tool"),
        20 => Said.Of("stand.type.atgeir"),
        21 => Said.Of("stand.type.fish"),
        24 => Said.Of("stand.type.trinket"),
        _ => Said.Of("stand.type.item"),
    };

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
        int q = quality, v = variant;
        if (itemData is { Length: > 2 })
        {
            try
            {
                using BinaryReader r = new(new MemoryStream(itemData), Encoding.UTF8);
                var it = InventoryData.ReadItem(r, r.ReadByte());
                q = it.Quality;
                v = it.Variant;
            }
            catch (Exception ex) when (CheckedReads.Damaged(ex))
            {
                // Kept as the stand's own values say.
                Log.CaughtBrief(ex, "reading the item a stand holds (damaged: the stand's own values are kept)");
            }
        }
        return new Held(PrefabCatalog.NameOf(hash) ?? I(hash), q, v);
    }

    /// <summary>An int value by key name, or <paramref name="otherwise"/> when the object has none.</summary>
    private static int Int(ZdoData z, string key, int otherwise = 0) => z.GetInt(StableHash.Of(key)) ?? otherwise;

    /// <summary>A number as invariant text.</summary>
    private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
}

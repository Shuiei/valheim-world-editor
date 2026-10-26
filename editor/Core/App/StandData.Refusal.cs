using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

public static partial class StandData
{
    /// <summary>Why this item stand does not take the item (ItemStand.CanAttach), in both languages, or null when it does.</summary>
    /// <param name="stand">The stand's prefab hash.</param>
    /// <param name="item">The item's prefab name.</param>
    public static Said? ItemStandRefusal(int stand, string item)
    {
        Said standName = StandName(stand);
        if (PrefabCatalog.ItemStandOf(stand) is not { } rule)
        {
            return Said.Of("stand.msg.notItemStand", standName);
        }
        if (PrefabCatalog.ItemKindOf(StableHash.Of(item)) is not { } kind)
        {
            return Said.Of("stand.msg.notAnItem", item);
        }
        if (!kind.Attach)
        {
            return Said.Of("stand.msg.cannotHang", item);
        }
        if (rule.Refuses.Contains(item))
        {
            return Said.Of("stand.msg.refuses", standName, item);
        }
        if (rule.Takes.Contains(item) || rule.Types.Contains(kind.Type))
        {
            return null;
        }
        return Said.Of("stand.msg.doesNotTake", standName, item, TypeWords(kind.Type), Said.Join(", ", rule.Types.Select(TypeWords).Distinct()));
    }

    /// <summary>The stand's name as the game shows it, or "This object" for a prefab the catalogue does not name.</summary>
    /// <param name="stand">The stand's prefab hash.</param>
    private static Said StandName(int stand) =>
        PrefabCatalog.DisplayName(stand) is { } name ? Said.Raw(name) : Said.Of("stand.thisObject");

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
}

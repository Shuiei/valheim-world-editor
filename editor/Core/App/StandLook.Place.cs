using static TerrainEditor.App.GameBundles;

namespace TerrainEditor.App;

/// <summary>
/// Where an item goes on its stand (StandLook.Place.cs): the matrix of the held model relative to the stand's root,
/// in Unity's space (row-major, column vectors), as the game places the item's attach child.
/// </summary>
public static partial class StandLook
{
    /// <summary>
    /// An item on an item stand: at the stand's attach point, with the attach child's own transform, the item's
    /// scale by quality and the item's offset for the stand's orientation (ItemStand.SetVisualItem).
    /// </summary>
    /// <param name="stand">The stand's points (an item stand's).</param>
    /// <param name="held">The item's held model.</param>
    /// <param name="quality">The item's quality.</param>
    /// <param name="orientation">The way it hangs (the stand's "type" value): which of the matching offsets.</param>
    /// <returns>The matrix, or null for a stand without an attach point.</returns>
    public static float[]? OnItemStand(StandPoints stand, HeldModel held, int quality, int orientation)
    {
        if (stand.Attach == null)
        {
            return null;
        }
        var matching = held.Offsets.Where(o => (o.Orientations & stand.Orientation) != 0).ToList();
        var offset = matching.Count > 0 ? matching[Math.Clamp(orientation, 0, matching.Count - 1)] : new StandOffset(3, [0, 0, 0], [0, 0, 0], [1, 1, 1]);
        float q = 1 + Math.Max(0, quality - 1) * held.ScaleByQuality;
        float[] position = [held.Position[0] + offset.Position[0], held.Position[1] + offset.Position[1], held.Position[2] + offset.Position[2]];
        float[] rotation = Mul(held.Rotation, Euler(offset.Rotation));
        float[] scale = [held.Scale[0] * q * offset.Scale[0], held.Scale[1] * q * offset.Scale[1], held.Scale[2] * q * offset.Scale[2]];
        return Matrix.Mul(stand.Attach, Trs(position, rotation, scale));
    }

    /// <summary>
    /// An item in an armour stand's slot: at the slot's joint (its position and turn, not its scale), keeping the
    /// attach child's own scale and its equip offset (VisEquipment.AttachItem); skinned armour on the body with its own
    /// transform (VisEquipment.AttachArmor).
    /// </summary>
    /// <param name="stand">The stand's points (an armour stand's).</param>
    /// <param name="held">The item's held model.</param>
    /// <param name="slot">The slot's index on the stand.</param>
    /// <param name="itemType">The item's type as it attaches (its attach override when it has one), which picks the back joint.</param>
    /// <returns>The matrix, or null when the stand has no such slot or no joint for it.</returns>
    public static float[]? OnArmourStand(StandPoints stand, HeldModel held, int slot, int itemType)
    {
        if (JointFor(stand, slot, itemType, held.Skin) is not { } joint)
        {
            return null;
        }
        float[] local = Trs(held.EquipOffset?.Position ?? [0, 0, 0], held.EquipOffset?.Rotation ?? [0, 0, 0, 1], held.Scale);
        return Matrix.Mul(held.Skin ? joint : Unscaled(joint), local);
    }

    /// <summary>The joint's matrix an item in a slot goes to; null when the stand has no such slot or no joint for it.</summary>
    private static float[]? JointFor(StandPoints stand, int slot, int itemType, bool skin)
    {
        if (stand.Slots == null || slot < 0 || slot >= stand.Slots.Count)
        {
            return null;
        }
        return JointOf(stand.Slots[slot], itemType, skin) is { } name && stand.Joints.TryGetValue(name, out float[]? joint) ? joint : null;
    }

    /// <summary>
    /// The joint a slot's item goes to: skinned armour to the body; hands and head to their joints; the back by the
    /// item's type (VisEquipment.AttachBackItem). Null: nowhere (a type with no back joint).
    /// </summary>
    private static string? JointOf(int visSlot, int itemType, bool skin) => (visSlot, skin) switch
    {
        (_, true) => "body",
        (0, _) => "leftHand",
        (1, _) => "rightHand",
        (6 or 9 or 10, _) => "helmet",
        (2 or 3, _) => BackJoint(itemType, visSlot == 3),
        _ => null,
    };

    /// <summary>The back joint an item of this type hangs from; null for a type that goes on no back.</summary>
    private static string? BackJoint(int itemType, bool right) => itemType switch
    {
        15 => right ? "backMelee" : "backTool",
        4 => "backBow",
        19 => "backTool",
        20 => "backAtgeir",
        3 => "backMelee",
        14 or 22 => "backTwohandedMelee",
        5 => "backShield",
        _ => null,
    };
}

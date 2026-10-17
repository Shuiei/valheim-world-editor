using System.Text;

namespace TerrainEditor.Save;

/// <summary>
/// Reads the contents of a container (see <see cref="InventoryData"/>): the header and each item.
/// </summary>
public sealed partial class InventoryData
{
    /// <summary>Parses an inventory byte array.</summary>
    /// <exception cref="NotSupportedException">The item format is older than 108.</exception>
    public static InventoryData Read(byte[] bytes)
    {
        using BinaryReader r = new(new MemoryStream(bytes), Encoding.UTF8);
        int version = r.ReadInt32();
        if (version < 108)
        {
            throw new NotSupportedException($"item format {version} is older than the editor reads (108)");
        }
        InventoryData inv = new();
        int count = r.ReadUInt16();
        for (int i = 0; i < count; i++)
        {
            inv.Items.Add(ReadItem(r, version));
        }
        return inv;
    }

    /// <summary>
    /// One item as ItemDrop.ItemData.Save writes it (in an inventory, or alone after a version byte in an item stand's
    /// or armour stand slot's "itemData"), in that item format version.
    /// </summary>
    /// <param name="r">Reader positioned at the item.</param>
    /// <param name="version">The item format version the bytes were written in.</param>
    public static Item ReadItem(BinaryReader r, int version)
    {
        Item it = new() { Durability = r.ReadInt32() * 0.01f, X = r.ReadByte(), Y = r.ReadByte(), WorldLevel = r.ReadByte() };
        byte b = r.ReadByte();
        it.PickedUp = (b & 1) != 0;
        it.Equipped = (b & 2) != 0;
        it.Quality = (b & 4) != 0 ? r.ReadUInt16() : 1;
        it.Stack = (b & 8) != 0 ? r.ReadUInt16() : 1;
        it.Variant = (b & 0x10) != 0 ? r.ReadInt32() : 0;
        if ((b & 0x20) != 0)
        {
            it.CrafterId = r.ReadInt64();
            it.CrafterName = r.ReadCheckedString();
        }
        it.Prefab = (b & 0x40) != 0 ? r.ReadInt32() : 0;
        int custom = (b & 0x80) != 0 ? r.ReadNumItems() : 0;
        for (int c = 0; c < custom; c++)
        {
            it.CustomData[r.ReadCheckedString()] = r.ReadCheckedString();
        }
        if (version is >= 109 or 107)
        {
            it.Cheated = (r.ReadByte() & 1) != 0;
        }
        return it;
    }
}

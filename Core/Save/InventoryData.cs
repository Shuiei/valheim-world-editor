using System.Text;

namespace TerrainEditor.Save;

/// <summary>
/// The contents of a container (the "items" byte array of chests, carts and ships), as Inventory.Save writes it since
/// item format 108: int version, ushort count, then per item ItemDrop.ItemData.Save. Older formats cannot be read by
/// the editor (Read throws NotSupportedException).
/// </summary>
public sealed class InventoryData
{
    /// <summary>The item format version Write and WriteItem produce.</summary>
    public const int Version = 109;

    /// <summary>One item stack in an inventory or an item stand slot.</summary>
    public sealed class Item
    {
        /// <summary>The item's prefab name hash (0 when the save left it out).</summary>
        public int Prefab { get; set; }

        /// <summary>How many items are in the stack (1 by default).</summary>
        public int Stack { get; set; } = 1;

        /// <summary>Durability points; saved as an int of hundredths.</summary>
        public float Durability { get; set; } = 100f;

        /// <summary>Grid column in the container.</summary>
        public int X { get; set; }

        /// <summary>Grid row in the container.</summary>
        public int Y { get; set; }

        /// <summary>The world level the item was made at (saved as one byte).</summary>
        public int WorldLevel { get; set; }

        /// <summary>The game's picked-up flag (bit 0 of the flags byte).</summary>
        public bool PickedUp { get; set; }

        /// <summary>Whether the item is equipped.</summary>
        public bool Equipped { get; set; }

        /// <summary>Upgrade level (1 by default).</summary>
        public int Quality { get; set; } = 1;

        /// <summary>Style variant index (0 by default).</summary>
        public int Variant { get; set; }

        /// <summary>
        /// Player id of the crafter (0 when nobody crafted it, and then the crafter name is not saved).
        /// </summary>
        public long CrafterId { get; set; }

        /// <summary>Name of the crafter, saved only with a non-zero <c>CrafterId</c>.</summary>
        public string CrafterName { get; set; } = "";

        /// <summary>Free-form key/value data mods and the game attach to the item.</summary>
        public Dictionary<string, string> CustomData { get; set; } = [];

        /// <summary>The item's cheated flag (bit 0 of a trailing byte, saved by item format 107 and 109+).</summary>
        public bool Cheated { get; set; }
    }

    /// <summary>The items in save order.</summary>
    public List<Item> Items { get; } = [];

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
            it.CrafterName = r.ReadString();
        }
        it.Prefab = (b & 0x40) != 0 ? r.ReadInt32() : 0;
        int custom = (b & 0x80) != 0 ? ReadNumItems(r) : 0;
        for (int c = 0; c < custom; c++)
        {
            it.CustomData[r.ReadString()] = r.ReadString();
        }
        if (version is >= 109 or 107)
        {
            it.Cheated = (r.ReadByte() & 1) != 0;
        }
        return it;
    }

    /// <summary>The inventory as a byte array in the current item format (<c>Version</c>).</summary>
    public byte[] Write()
    {
        using MemoryStream ms = new();
        using BinaryWriter w = new(ms, Encoding.UTF8);
        w.Write(Version);
        w.Write((ushort)Items.Count);
        foreach (Item it in Items)
        {
            WriteItem(w, it);
        }
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>One item as ItemDrop.ItemData.Save writes it, in the current item format (Version).</summary>
    public static void WriteItem(BinaryWriter w, Item it)
    {
        int flags = (it.PickedUp ? 1 : 0) | (it.Equipped ? 2 : 0) | (it.Quality != 1 ? 4 : 0) | (it.Stack != 1 ? 8 : 0)
            | (it.Variant != 0 ? 0x10 : 0) | (it.CrafterId != 0 ? 0x20 : 0) | (it.Prefab != 0 ? 0x40 : 0) | (it.CustomData.Count != 0 ? 0x80 : 0);
        w.Write((int)(it.Durability * 100f));
        w.Write((byte)it.X);
        w.Write((byte)it.Y);
        w.Write((byte)it.WorldLevel);
        w.Write((byte)flags);
        if ((flags & 4) != 0)
        {
            w.Write((ushort)it.Quality);
        }

        if ((flags & 8) != 0)
        {
            w.Write((ushort)it.Stack);
        }

        if ((flags & 0x10) != 0)
        {
            w.Write(it.Variant);
        }

        if ((flags & 0x20) != 0) { w.Write(it.CrafterId); w.Write(it.CrafterName); }
        if ((flags & 0x40) != 0)
        {
            w.Write(it.Prefab);
        }

        if ((flags & 0x80) != 0)
        {
            ZdoBuilder.WriteNumItems(w, it.CustomData.Count);
            foreach (var (k, v) in it.CustomData)
            {
                w.Write(k);
                w.Write(v);
            }
        }
        w.Write((byte)(it.Cheated ? 1 : 0));
    }

    /// <summary>
    /// A count in the game's compact form: one byte below 128, else two bytes, the first with its high bit set.
    /// </summary>
    private static int ReadNumItems(BinaryReader r)
    {
        int n = r.ReadByte();
        return (n & 0x80) != 0 ? ((n & 0x7F) << 8) | r.ReadByte() : n;
    }
}

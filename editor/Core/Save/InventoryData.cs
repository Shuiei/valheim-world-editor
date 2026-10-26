namespace TerrainEditor.Save;

/// <summary>
/// The contents of a container (the "items" byte array of chests, carts and ships), as Inventory.Save writes it since
/// item format 108: int version, ushort count, then per item ItemDrop.ItemData.Save. Older formats cannot be read by
/// the editor (Read throws NotSupportedException).
/// </summary>
public sealed partial class InventoryData
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
}

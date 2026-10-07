using System.Text;

namespace TerrainEditor.Save;

// The contents of a container (the "items" byte array of chests, carts and ships), as Inventory.Save
// writes it since item format 108: int version, ushort count, then per item ItemDrop.ItemData.Save.
// Older formats are read-only for the editor (Read throws NotSupportedException).
public sealed class InventoryData
{
	public const int Version = 109;

	public sealed class Item
	{
		public int Prefab { get; set; }

		public int Stack { get; set; } = 1;

		public float Durability { get; set; } = 100f;

		public int X { get; set; }

		public int Y { get; set; }

		public int WorldLevel { get; set; }

		public bool PickedUp { get; set; }

		public bool Equipped { get; set; }

		public int Quality { get; set; } = 1;

		public int Variant { get; set; }

		public long CrafterId { get; set; }

		public string CrafterName { get; set; } = "";

		public Dictionary<string, string> CustomData { get; set; } = new();

		public bool Cheated { get; set; }
	}

	public List<Item> Items { get; } = new();

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
			if (version >= 109 || version == 107)
			{
				it.Cheated = (r.ReadByte() & 1) != 0;
			}
			inv.Items.Add(it);
		}
		return inv;
	}

	public byte[] Write()
	{
		using MemoryStream ms = new();
		using BinaryWriter w = new(ms, Encoding.UTF8);
		w.Write(Version);
		w.Write((ushort)Items.Count);
		foreach (Item it in Items)
		{
			int flags = (it.PickedUp ? 1 : 0) | (it.Equipped ? 2 : 0) | (it.Quality != 1 ? 4 : 0) | (it.Stack != 1 ? 8 : 0)
				| (it.Variant != 0 ? 0x10 : 0) | (it.CrafterId != 0 ? 0x20 : 0) | (it.Prefab != 0 ? 0x40 : 0) | (it.CustomData.Count != 0 ? 0x80 : 0);
			w.Write((int)(it.Durability * 100f));
			w.Write((byte)it.X);
			w.Write((byte)it.Y);
			w.Write((byte)it.WorldLevel);
			w.Write((byte)flags);
			if ((flags & 4) != 0) w.Write((ushort)it.Quality);
			if ((flags & 8) != 0) w.Write((ushort)it.Stack);
			if ((flags & 0x10) != 0) w.Write(it.Variant);
			if ((flags & 0x20) != 0) { w.Write(it.CrafterId); w.Write(it.CrafterName); }
			if ((flags & 0x40) != 0) w.Write(it.Prefab);
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
		w.Flush();
		return ms.ToArray();
	}

	private static int ReadNumItems(BinaryReader r)
	{
		int n = r.ReadByte();
		return (n & 0x80) != 0 ? ((n & 0x7F) << 8) | r.ReadByte() : n;
	}
}

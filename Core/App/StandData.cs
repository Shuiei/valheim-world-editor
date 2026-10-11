using System.Globalization;
using System.Text;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// What item stands and armour stands hold, as the game saves it. An item stand (ItemStand: itemstand,
// itemstandh, the boss altars) keeps the item's prefab hash in "item", with its "variant", its
// "quality" and the way it hangs in "type"; an armour stand (ArmorStand) keeps slot i's item in
// "<i>_item" and "<i>_variant", and the stand's "pose". Both keep the whole item as
// ItemDrop.SaveToZDO writes it ("itemData", "<i>_itemData": the item format version in one byte, then
// the item as an inventory keeps it): that is what a player gets back on taking it.
public static class StandData
{
	// An item on a stand: its prefab name, quality (upgrade level) and variant (a shield's style).
	public sealed record Held(string Item, int Quality = 1, int Variant = 0);

	public static bool IsItemStand(int prefab) => PrefabCatalog.ItemStandOf(prefab) != null;

	public static bool IsArmourStand(int prefab) => PrefabCatalog.ArmourSlotsOf(prefab) != null;

	// ItemDrop.ItemData.ItemType, as words.
	public static string TypeName(int type) => type switch
	{
		1 => "material",
		2 => "food or potion",
		3 => "one-handed weapon",
		4 => "bow",
		5 => "shield",
		6 => "helmet",
		7 => "chest armour",
		9 => "ammo",
		10 => "customization",
		11 => "legs armour",
		12 => "hands",
		13 => "trophy",
		14 => "two-handed weapon",
		15 => "torch",
		16 => "misc item",
		17 => "cape",
		18 => "utility item",
		19 => "tool",
		20 => "atgeir",
		21 => "fish",
		22 => "two-handed weapon",
		23 => "ammo",
		24 => "trinket",
		_ => "item",
	};

	// Why this item stand does not take the item (ItemStand.CanAttach), or null when it does.
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

	// Whether an armour stand's slot takes the item (ArmorStand.CanAttach, and its check that anything
	// but chest and legs pieces has an attach point).
	public static bool FitsArmourSlot(int[] slotTypes, PrefabCatalog.ItemKind kind)
	{
		int type = kind.AttachOverride != 0 ? kind.AttachOverride : kind.Type;
		if (slotTypes.Length > 0 && !slotTypes.Contains(type))
		{
			return false;
		}
		return kind.Type is 7 or 11 || kind.Attach || kind.AttachSkin;
	}

	// The values that make an item stand hold this item (null: nothing), hanging this way (the game's
	// orientations: 0 is the first). Throws ArgumentException when the stand does not take the item.
	public static List<FieldChange> ForItemStand(int stand, Held? held, int orientation)
	{
		if (held == null)
		{
			// As ItemStand.DropItem leaves it.
			return new() { new("ints", "item", "0"), new("ints", "type", "0"), new("bytes", "itemData", null) };
		}
		if (ItemStandRefusal(stand, held.Item) is string why)
		{
			throw new ArgumentException(why);
		}
		int hash = StableHash.Of(held.Item);
		var kind = PrefabCatalog.ItemKindOf(hash)!;
		int quality = Math.Clamp(held.Quality, 1, 10), variant = Variant(kind, held.Variant);
		return new()
		{
			new("ints", "item", I(hash)), new("ints", "variant", I(variant)), new("ints", "quality", I(quality)),
			new("ints", "type", I(Math.Max(0, orientation))), new("bytes", "itemData", ItemData(hash, kind, quality, variant)),
		};
	}

	// The values that make an armour stand wear these items (each in the first free slot that takes it;
	// the other slots emptied), in this pose when given. Throws ArgumentException when one fits nowhere.
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

	// What an item stand holds (null: nothing) and the way it hangs.
	public static (Held? Item, int Orientation) ReadItemStand(ZdoData z)
	{
		int hash = Int(z, "item");
		return (hash == 0 ? null : HeldOf(hash, Int(z, "quality", 1), Int(z, "variant"), z.GetBytes(StableHash.Of("itemData"))), Int(z, "type"));
	}

	// What each slot of an armour stand holds (null: nothing), and its pose.
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
			catch (Exception ex) when (ex is EndOfStreamException or IOException)
			{
				// Kept as the stand's own values say.
			}
		}
		return new Held(PrefabCatalog.NameOf(hash) ?? I(hash), quality, variant);
	}

	private static int Variant(PrefabCatalog.ItemKind kind, int variant) => kind.Variants > 0 ? Math.Clamp(variant, 0, kind.Variants - 1) : 0;

	// ItemDrop.SaveToZDO's bytes for a new item: the item format version, then the item.
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

	private static int Int(ZdoData z, string key, int otherwise = 0)
	{
		int k = StableHash.Of(key);
		foreach (var (kk, v) in z.IntList)
		{
			if (kk == k)
			{
				return v;
			}
		}
		return otherwise;
	}

	private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
}

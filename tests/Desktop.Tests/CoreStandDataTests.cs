using System.Text;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// What item stands and armour stands hold (StandData): what the game lets each take (ItemStand.CanAttach,
// ArmorStand.CanAttach), the values written as the game writes them, and reading them back.
public class CoreStandDataTests
{
	private static readonly int Wall = StableHash.Of("itemstand"), Flat = StableHash.Of("itemstandh"), Armour = StableHash.Of("ArmorStand");

	private static ZdoData With(List<FieldChange> set)
	{
		var z = new ZdoData();
		foreach (var f in set)
		{
			z.Set(f.Section, StableHash.Of(f.Key), f.Value);
		}
		return z;
	}

	private static InventoryData.Item ItemOf(string base64)
	{
		byte[] bytes = Convert.FromBase64String(base64);
		using var r = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
		Assert.Equal(InventoryData.Version, r.ReadByte());
		var it = InventoryData.ReadItem(r, InventoryData.Version);
		Assert.Equal(bytes.Length, r.BaseStream.Position);
		return it;
	}

	[Fact]
	public void StandsTakeWhatTheGameLetsThem()
	{
		Assert.True(StandData.IsItemStand(Wall));
		Assert.True(StandData.IsItemStand(Flat));
		Assert.True(StandData.IsArmourStand(Armour));
		Assert.False(StandData.IsItemStand(StableHash.Of("piece_chest_wood")));
		Assert.Null(StandData.ItemStandRefusal(Wall, "SwordIron"));
		Assert.Null(StandData.ItemStandRefusal(Wall, "TrophyDeer"));
		Assert.Null(StandData.ItemStandRefusal(Wall, "SurtlingCore")); // a material, but on the stand's own list
		Assert.Contains("does not take", StandData.ItemStandRefusal(Wall, "CookedMeat")); // food: only lying flat
		Assert.Null(StandData.ItemStandRefusal(Flat, "CookedMeat"));
		Assert.Contains("refuses", StandData.ItemStandRefusal(Wall, "MeadTasty"));
		Assert.Contains("cannot hang", StandData.ItemStandRefusal(Flat, "Wood")); // no attach point
		Assert.Contains("not an item of the game", StandData.ItemStandRefusal(Wall, "NoSuchThing"));
		Assert.Contains("not an item stand", StandData.ItemStandRefusal(Armour, "SwordIron"));
	}

	[Fact]
	public void AnItemStandHoldsTheItemAsTheGameSavesIt()
	{
		var set = StandData.ForItemStand(Wall, new StandData.Held("SwordIron", Quality: 3), orientation: 1);
		var z = With(set);
		Assert.Equal(StableHash.Of("SwordIron"), z.IntList.Single(i => i.Key == StableHash.Of("item")).Value);
		Assert.Equal(3, z.IntList.Single(i => i.Key == StableHash.Of("quality")).Value);
		Assert.Equal(1, z.IntList.Single(i => i.Key == StableHash.Of("type")).Value);
		var it = ItemOf(set.Single(f => f.Key == "itemData").Value!);
		Assert.Equal(StableHash.Of("SwordIron"), it.Prefab);
		Assert.Equal(3, it.Quality);
		Assert.Equal(300f, it.Durability); // new: 200, and 50 for each level after the first
		Assert.Equal((new StandData.Held("SwordIron", 3, 0), 1), StandData.ReadItemStand(z));
		// A shield's style stays within its variants; emptied, the stand is as the game leaves it.
		Assert.Equal(3, ItemOf(StandData.ForItemStand(Wall, new StandData.Held("ShieldWood", Variant: 9), 0).Single(f => f.Key == "itemData").Value!).Variant);
		var empty = With(StandData.ForItemStand(Wall, null, 0));
		Assert.Equal(((StandData.Held?)null, 0), StandData.ReadItemStand(empty));
		Assert.Null(empty.GetBytes(StableHash.Of("itemData")));
		Assert.Throws<ArgumentException>(() => StandData.ForItemStand(Wall, new StandData.Held("Wood"), 0));
	}

	[Fact]
	public void AnArmourStandWearsEachItemInItsSlot()
	{
		int[][] slots = PrefabCatalog.ArmourSlotsOf(Armour)!;
		var held = new[] { new StandData.Held("SwordIron"), new StandData.Held("HelmetBronze", 2), new StandData.Held("ArmorBronzeChest"), new StandData.Held("CapeDeerHide"), new StandData.Held("ShieldWood", Variant: 1) };
		var z = With(StandData.ForArmourStand(Armour, held, pose: 2));
		var (worn, pose) = StandData.ReadArmourStand(z, slots.Length);
		Assert.Equal(2, pose);
		Assert.Equal(held.Length, worn.Count(h => h != null));
		foreach (var h in held)
		{
			int slot = Array.FindIndex(worn, w => w?.Item == h.Item);
			var kind = PrefabCatalog.ItemKindOf(StableHash.Of(h.Item))!;
			Assert.True(StandData.FitsArmourSlot(slots[slot], kind), $"{h.Item} in slot {slot}");
			Assert.Equal(h.Quality, worn[slot]!.Quality);
			Assert.Equal(h.Variant, worn[slot]!.Variant);
		}
		Assert.Throws<ArgumentException>(() => StandData.ForArmourStand(Armour, new[] { new StandData.Held("CookedMeat") }, null));
		// ArmorStand has two helmet slots: a third helmet fits nowhere.
		Assert.Contains("no free slot", Assert.Throws<ArgumentException>(() => StandData.ForArmourStand(Armour, Enumerable.Repeat(new StandData.Held("HelmetBronze"), 3).ToList(), null)).Message);
		// Wearing less empties the other slots.
		var after = With(StandData.ForArmourStand(Armour, new[] { new StandData.Held("HelmetBronze") }, null));
		Assert.Single(StandData.ReadArmourStand(after, slots.Length).Slots, h => h != null);
	}
}

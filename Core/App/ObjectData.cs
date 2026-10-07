using System.Globalization;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// An object's saved data, for the inspector of both editors: its bytes (also for objects added in this
// session), and a container's contents built from a list of items.
public static class ObjectData
{
	public static readonly int ItemsKey = StableHash.Of("items");

	// The saved bytes of an object of the world (id >= 0) or of one added in this session (id < 0).
	public static byte[]? Bytes(WorldSave w, EditStore edits, int id)
	{
		if (id >= 0)
		{
			return id < w.ObjectRefs.Count ? w.ObjectBytes(id) : null;
		}
		NewObject? n = edits.FindAdded(id);
		if (n == null)
		{
			return null;
		}
		Dictionary<ChunkFile, byte[]> files = new();
		return w.NewObjectBytes(n, m => w.LiveBytes ?? (files.TryGetValue(m.File, out byte[]? f) ? f : files[m.File] = File.ReadAllBytes(Path.Combine(w.Directory, m.File.FileName))));
	}

	public static InventoryData BuildInventory(List<ItemUpload> list)
	{
		InventoryData inv = new();
		foreach (ItemUpload u in list)
		{
			int prefab = u.Prefab ?? StableHash.Of(u.Name ?? "");
			if (PrefabCatalog.NameOf(prefab) == null)
			{
				throw new ArgumentException($"“{u.Name ?? prefab.ToString(CultureInfo.InvariantCulture)}” is not an item of the game.");
			}
			inv.Items.Add(new InventoryData.Item
			{
				Prefab = prefab, Stack = Math.Clamp(u.Stack, 1, 65535), Quality = Math.Clamp(u.Quality, 1, 65535), Durability = u.Durability, X = Math.Clamp(u.X, 0, 255), Y = Math.Clamp(u.Y, 0, 255),
				Variant = u.Variant, WorldLevel = Math.Clamp(u.WorldLevel, 0, 255), CrafterId = long.TryParse(u.CrafterId, NumberStyles.Integer, CultureInfo.InvariantCulture, out long c) ? c : 0,
				CrafterName = u.CrafterName ?? "", Equipped = u.Equipped, PickedUp = u.PickedUp, Cheated = u.Cheated, CustomData = u.CustomData ?? new(),
			});
		}
		return inv;
	}

	// The object with these values set (null removes one) and these contents: throws FormatException,
	// ArgumentException or OverflowException for values that do not fit.
	public static ZdoData Edited(byte[] bytes, List<FieldChange>? set, List<ItemUpload>? inventory)
	{
		ZdoData z = ZdoData.Parse(bytes);
		foreach (FieldChange f in set ?? new())
		{
			z.Set(f.Section, ZdoKeys.Parse(f.Key), f.Value);
		}
		if (inventory != null)
		{
			z.SetBytes(ItemsKey, BuildInventory(inventory).Write());
		}
		return z;
	}
}

public sealed record FieldChange(string Section, string Key, string? Value);

public sealed record ItemUpload(string? Name, int? Prefab, int Stack = 1, int Quality = 1, float Durability = 100f, int X = 0, int Y = 0, int Variant = 0, int WorldLevel = 0,
	string? CrafterId = null, string? CrafterName = null, bool Equipped = false, bool PickedUp = false, bool Cheated = false, Dictionary<string, string>? CustomData = null);


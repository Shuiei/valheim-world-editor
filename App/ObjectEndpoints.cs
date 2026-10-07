using System.Globalization;
using System.Text.Json.Nodes;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

// The object inspector (like MCEdit's NBT editor): everything an object holds in the save, by name,
// and editing it. An edit replaces the object by a copy with the changed data (like a move does), so
// undo, saving and live apply work as for any other change.
public static class ObjectEndpoints
{
	private static readonly int ItemsKey = StableHash.Of("items");

	public static void Map(WebApplication app, Func<WorldSave> world, EditStore edits, Func<object> pending)
	{
		app.MapGet("/api/items", () => PrefabCatalog.Items);

		app.MapGet("/api/object/{id:int}", (int id) =>
		{
			byte[]? bytes = Bytes(world(), edits, id);
			return bytes == null ? Results.NotFound() : Results.Json(Describe(id, ZdoData.Parse(bytes)));
		});

		app.MapPost("/api/objects/edit", (ObjectEdit req) =>
		{
			WorldSave w = world();
			byte[]? bytes = Bytes(w, edits, req.Id);
			if (bytes == null)
			{
				return Results.NotFound();
			}
			if (req.NewId >= 0)
			{
				return Results.BadRequest("The new object needs a negative id.");
			}
			ZdoData z = ZdoData.Parse(bytes);
			try
			{
				foreach (FieldChange f in req.Set ?? new())
				{
					z.Set(f.Section, ZdoKeys.Parse(f.Key), f.Value);
				}
				if (req.Inventory != null)
				{
					z.SetBytes(ItemsKey, ObjectData.BuildInventory(req.Inventory).Write());
				}
			}
			catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
			{
				return Results.BadRequest(ex.Message);
			}
			byte[] raw = z.Serialize();
			edits.AddObjects(new[] { new NewObject(req.NewId, z.Prefab, z.Position, z.Rotation, 0f, null, false, raw) });
			edits.SetDeleted(new[] { req.Id }, true);
			return Results.Json(new { pending = pending(), detail = Describe(req.NewId, z) });
		});
	}

	// The saved bytes of an object (ObjectData, in the shared library).
	public static byte[]? Bytes(WorldSave w, EditStore edits, int id) => ObjectData.Bytes(w, edits, id);

	public static string? PrefabName(int prefab) => PrefabCatalog.DisplayName(prefab);

	private static string S(float v) => v.ToString("R", CultureInfo.InvariantCulture);

	public static JsonObject Describe(int id, ZdoData z)
	{
		JsonArray fields = new();
		void Add(string section, int key, JsonNode? value, string? note = null)
		{
			JsonObject f = new() { ["section"] = section, ["key"] = key, ["name"] = ZdoKeys.NameOf(key), ["value"] = value };
			if (note != null)
			{
				f["note"] = note;
			}
			fields.Add(f);
		}
		foreach (var (k, v) in z.FloatList) Add("floats", k, v);
		foreach (var (k, v) in z.Vec3List) Add("vec3", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)}");
		foreach (var (k, v) in z.QuatList) Add("quats", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)} {S(v.W)}");
		foreach (var (k, v) in z.IntList)
		{
			// Ints that are prefab or item hashes (item stands, cooking stations...) get the name beside them.
			Add("ints", k, v, PrefabCatalog.NameOf(v));
		}
		foreach (var (k, v) in z.LongList) Add("longs", k, v.ToString(CultureInfo.InvariantCulture));
		foreach (var (k, v) in z.StringList) Add("strings", k, v);
		foreach (var (k, v) in z.ByteList) Add("bytes", k, Convert.ToBase64String(v), $"{v.Length} bytes");
		PrefabCatalog.Info? details = PrefabCatalog.Details(z.Prefab);
		JsonObject? inventory = null;
		byte[]? items = z.GetBytes(ItemsKey);
		if (items != null || details?.ContainerW > 0)
		{
			inventory = new JsonObject { ["width"] = details?.ContainerW ?? 0, ["height"] = details?.ContainerH ?? 0 };
			try
			{
				InventoryData inv = items != null ? InventoryData.Read(items) : new InventoryData();
				inventory["items"] = new JsonArray(inv.Items.Select(i => (JsonNode)new JsonObject
				{
					["name"] = PrefabCatalog.NameOf(i.Prefab) ?? i.Prefab.ToString(CultureInfo.InvariantCulture),
					["prefab"] = i.Prefab, ["stack"] = i.Stack, ["quality"] = i.Quality, ["durability"] = i.Durability, ["x"] = i.X, ["y"] = i.Y,
					["variant"] = i.Variant, ["worldLevel"] = i.WorldLevel, ["crafterId"] = i.CrafterId.ToString(CultureInfo.InvariantCulture), ["crafterName"] = i.CrafterName,
					["equipped"] = i.Equipped, ["pickedUp"] = i.PickedUp, ["cheated"] = i.Cheated,
					["customData"] = new JsonObject(i.CustomData.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
				}).ToArray());
			}
			catch (Exception ex) when (ex is NotSupportedException or EndOfStreamException or IOException)
			{
				inventory["error"] = ex.Message;
			}
		}
		return new JsonObject
		{
			["id"] = id, ["prefab"] = z.Prefab, ["name"] = PrefabName(z.Prefab),
			["x"] = z.Position.X, ["y"] = z.Position.Y, ["z"] = z.Position.Z, ["rx"] = z.Rotation.X, ["ry"] = z.Rotation.Y, ["rz"] = z.Rotation.Z,
			["persistent"] = (z.BaseFlags & 0x100) != 0, ["distant"] = (z.BaseFlags & 0x200) != 0, ["type"] = (z.BaseFlags >> 10) & 3,
			["connection"] = z.Connection != null,
			["fields"] = fields, ["inventory"] = inventory,
		};
	}
}

public sealed record ObjectEdit(int Id, int NewId, List<FieldChange>? Set, List<ItemUpload>? Inventory);

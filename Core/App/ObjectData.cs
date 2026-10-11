using System.Globalization;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// An object's saved data, for the inspector of both editors: its bytes (also for objects added in this
/// session), and a container's contents built from a list of items.
/// </summary>
public static class ObjectData
{
    /// <summary>The byte-array key holding a container's inventory.</summary>
    public static readonly int ItemsKey = StableHash.Of("items");

    /// <summary>The int key telling a container it has had its default loot already.</summary>
    public static readonly int AddedDefaultItemsKey = StableHash.Of("addedDefaultItems");

    /// <summary>
    /// The saved bytes of an object of the world (id &gt;= 0) or of one added in this session (id &lt; 0); null
    /// when there is no such object.
    /// </summary>
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
        Dictionary<ChunkFile, byte[]> files = [];
        return w.NewObjectBytes(n, m => w.IsLive ? w.LiveSource(m.File) : (files.TryGetValue(m.File, out byte[]? f) ? f : files[m.File] = File.ReadAllBytes(Path.Combine(w.Directory, m.File.FileName))));
    }

    /// <summary>
    /// A container's contents from a list of items, each value clamped to what the save holds (stack and
    /// quality 1..65535, slot and world level 0..255).
    /// </summary>
    /// <exception cref="ArgumentException">An item is not one of the game's.</exception>
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
                Prefab = prefab,
                Stack = Math.Clamp(u.Stack, 1, 65535),
                Quality = Math.Clamp(u.Quality, 1, 65535),
                Durability = u.Durability,
                X = Math.Clamp(u.X, 0, 255),
                Y = Math.Clamp(u.Y, 0, 255),
                Variant = u.Variant,
                WorldLevel = Math.Clamp(u.WorldLevel, 0, 255),
                CrafterId = long.TryParse(u.CrafterId, NumberStyles.Integer, CultureInfo.InvariantCulture, out long c) ? c : 0,
                CrafterName = u.CrafterName ?? "",
                Equipped = u.Equipped,
                PickedUp = u.PickedUp,
                Cheated = u.Cheated,
                CustomData = u.CustomData ?? [],
            });
        }
        return inv;
    }

    /// <summary>
    /// The object with these values set (null removes one) and these contents: throws FormatException,
    /// ArgumentException or OverflowException for values that do not fit.
    /// </summary>
    /// <param name="bytes">The object's saved bytes.</param>
    /// <param name="set">Values to set or remove, if any.</param>
    /// <param name="inventory">
    /// The container's new contents (also marked as having had its default loot); null leaves them as they are.
    /// </param>
    public static ZdoData Edited(byte[] bytes, List<FieldChange>? set, List<ItemUpload>? inventory)
    {
        var z = ZdoData.Parse(bytes);
        foreach (FieldChange f in set ?? [])
        {
            z.Set(f.Section, ZdoKeys.Parse(f.Key), f.Value);
        }
        if (inventory != null)
        {
            z.SetBytes(ItemsKey, BuildInventory(inventory).Write());
            // Told it has its default items already: a chest never opened fills itself with the game's
            // loot when it first loads (Container.Awake), over what was set here.
            z.Set("ints", AddedDefaultItemsKey, "1");
        }
        return z;
    }
}

/// <summary>One saved value of an object to set or remove, as the inspector sends it.</summary>
/// <param name="Section">Which list it is in: "floats", "vec3", "quats", "ints", "longs", "strings" or "bytes".</param>
/// <param name="Key">Its key by name or as a hash in decimal ("-1234").</param>
/// <param name="Value">The value as text (numbers separated for vectors, base64 for bytes); null removes it.</param>
public sealed record FieldChange(string Section, string Key, string? Value);

/// <summary>One item of a container's contents, as the inspector or Claude sends it.</summary>
/// <param name="Name">The item's prefab name, used when <paramref name="Prefab"/> is null.</param>
/// <param name="Prefab">The item's prefab hash.</param>
/// <param name="Stack">How many (1..65535).</param>
/// <param name="Quality">Its upgrade level (1..65535).</param>
/// <param name="Durability">Its durability.</param>
/// <param name="X">Its slot's column (0..255).</param>
/// <param name="Y">Its slot's row (0..255).</param>
/// <param name="Variant">Its style variant.</param>
/// <param name="WorldLevel">The world level it was made at (0..255).</param>
/// <param name="CrafterId">Who crafted it, a player id as decimal text; 0 when missing or not a number.</param>
/// <param name="CrafterName">The crafter's name.</param>
/// <param name="Equipped">Whether it is equipped.</param>
/// <param name="PickedUp">The game's picked-up flag.</param>
/// <param name="Cheated">The game's cheated flag.</param>
/// <param name="CustomData">Mods' extra item data, key to value.</param>
public sealed record ItemUpload(string? Name, int? Prefab, int Stack = 1, int Quality = 1, float Durability = 100f, int X = 0, int Y = 0, int Variant = 0, int WorldLevel = 0,
    string? CrafterId = null, string? CrafterName = null, bool Equipped = false, bool PickedUp = false, bool Cheated = false, Dictionary<string, string>? CustomData = null);


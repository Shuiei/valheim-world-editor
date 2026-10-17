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
    public static byte[]? Bytes(WorldSave w, EditStore edits, int id) => Bytes(w, edits.FindAdded, id, WorldSave.Builder);

    /// <summary>
    /// <see cref="Bytes(WorldSave, EditStore, int)"/> from objects added taken earlier, with the builder taken with
    /// them: what a reader on another thread writes as the world and edits were then.
    /// </summary>
    /// <param name="w">The world.</param>
    /// <param name="findAdded">An object added in this session by id (null when there is none).</param>
    /// <param name="id">The object.</param>
    /// <param name="builder">The builder a new piece of a kind players build is given (0: none).</param>
    public static byte[]? Bytes(WorldSave w, Func<int, NewObject?> findAdded, int id, long builder)
    {
        if (id >= 0)
        {
            return id < w.ObjectRefs.Count ? w.ObjectBytes(id) : null;
        }
        NewObject? n = findAdded(id);
        if (n == null)
        {
            return null;
        }
        return w.NewObjectBytes(n, w.SourceReader(), builder);
    }

    /// <summary>The keys of an object's own scale, which a copy takes from its own size instead.</summary>
    private static readonly int ScaleKey = StableHash.Of("scale"), ScaleScalarKey = StableHash.Of("scaleScalar");

    /// <summary>
    /// Every value an object's saved bytes hold, as fields to set on a copy of it (a chest's contents, a sign's
    /// text, health, a creature's stars...), but its scale.
    /// </summary>
    /// <param name="bytes">The object's saved bytes.</param>
    /// <param name="builder">Its builder too ("creator"; with none, a field taking the copy's away).</param>
    public static List<ObjectField> Fields(byte[] bytes, bool builder)
    {
        var z = ZdoData.Parse(bytes);
        var ci = CultureInfo.InvariantCulture;
        static string S(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        var list = new List<ObjectField>();
        list.AddRange(z.FloatList.Where(f => f.Key != ScaleScalarKey).Select(f => new ObjectField("floats", f.Key, S(f.Value))));
        list.AddRange(z.Vec3List.Where(f => f.Key != ScaleKey).Select(f => new ObjectField("vec3", f.Key, $"{S(f.Value.X)} {S(f.Value.Y)} {S(f.Value.Z)}")));
        list.AddRange(z.QuatList.Select(f => new ObjectField("quats", f.Key, $"{S(f.Value.X)} {S(f.Value.Y)} {S(f.Value.Z)} {S(f.Value.W)}")));
        list.AddRange(z.IntList.Select(f => new ObjectField("ints", f.Key, f.Value.ToString(ci))));
        list.AddRange(z.LongList.Where(f => builder || f.Key != ObjectField.CreatorKey).Select(f => new ObjectField("longs", f.Key, f.Value.ToString(ci))));
        list.AddRange(z.StringList.Select(f => new ObjectField("strings", f.Key, f.Value)));
        list.AddRange(z.ByteList.Select(f => new ObjectField("bytes", f.Key, Convert.ToBase64String(f.Value))));
        if (builder && !z.LongList.Any(f => f.Key == ObjectField.CreatorKey))
        {
            list.Add(ObjectField.NoBuilder);
        }
        return list;
    }

    /// <summary>
    /// A container's contents from a list of items, each value clamped to what the save holds (stack and
    /// quality 1..65535, slot and world level 0..255).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An entry is not an item of the game: unknown, or a prefab that is not an item (a creature, a piece), which the
    /// game would drop from the container on load.
    /// </exception>
    /// <param name="list">The items.</param>
    /// <param name="notes">Told "Wood: stack of 70 cut to 50 (the item's maximum)" for each stack cut, when given.</param>
    public static InventoryData BuildInventory(List<ItemUpload> list, ICollection<string>? notes = null)
    {
        InventoryData inv = new();
        foreach (ItemUpload u in list)
        {
            int prefab = u.Prefab ?? StableHash.Of(u.Name ?? "");
            if (PrefabCatalog.ItemKindOf(prefab) is not { } kind)
            {
                throw new ArgumentException($"“{u.Name ?? prefab.ToString(CultureInfo.InvariantCulture)}” is not an item of the game.");
            }
            // The game keeps a stack's worth: a larger stack is cut to the item's largest (1: the item does not
            // stack). The catalogue always knows it: each build of the editor scans the game again.
            int stack = Math.Clamp(u.Stack, 1, 65535);
            if (stack > kind.MaxStack)
            {
                notes?.Add($"{u.Name ?? PrefabCatalog.NameOf(prefab) ?? prefab.ToString(CultureInfo.InvariantCulture)}: stack of {stack} cut to {kind.MaxStack} (the item's maximum)");
                stack = kind.MaxStack;
            }
            inv.Items.Add(new InventoryData.Item
            {
                Prefab = prefab,
                Stack = stack,
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
    /// <param name="notes">Told of stacks cut to the item's maximum (BuildInventory), when given.</param>
    public static ZdoData Edited(byte[] bytes, List<FieldChange>? set, List<ItemUpload>? inventory, ICollection<string>? notes = null)
    {
        var z = ZdoData.Parse(bytes);
        foreach (FieldChange f in set ?? [])
        {
            z.Set(f.Section, ZdoKeys.Parse(f.Key), f.Value);
        }
        if (inventory != null)
        {
            z.SetBytes(ItemsKey, BuildInventory(inventory, notes).Write());
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


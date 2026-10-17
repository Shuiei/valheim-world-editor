using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>Back to the object's data as opened: its values, contents and stand, nothing changed.</summary>
    private void Reset()
    {
        var z = _z!;
        var list = new List<Field>();
        void Add(string section, int key, string value, string? note = null) => list.Add(new Field { Section = section, Key = key, Name = ZdoKeys.NameOf(key), Value = value, Now = value, Note = note });
        foreach (var (k, v) in z.FloatList)
        {
            Add("floats", k, S(v));
        }

        foreach (var (k, v) in z.Vec3List)
        {
            Add("vec3", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)}");
        }

        foreach (var (k, v) in z.QuatList)
        {
            Add("quats", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)} {S(v.W)}");
        }
        // Ints that are prefab or item hashes (item stands, cooking stations…) get the name beside them.
        foreach (var (k, v) in z.IntList)
        {
            Add("ints", k, v.ToString(CultureInfo.InvariantCulture), PrefabCatalog.NameOf(v));
        }

        foreach (var (k, v) in z.LongList)
        {
            Add("longs", k, v.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var (k, v) in z.StringList)
        {
            Add("strings", k, v);
        }

        foreach (var (k, v) in z.ByteList)
        {
            Add("bytes", k, Convert.ToBase64String(v), Lang.T("inspect.bytes", v.Length));
        }

        FieldList = list;
        Added.Clear();
        var details = PrefabCatalog.Details(z.Prefab);
        byte[]? items = z.GetBytes(ObjectData.ItemsKey);
        Items = null;
        _inventoryError = null;
        _grid = (details?.ContainerW ?? 0, details?.ContainerH ?? 0);
        if (items != null || details?.ContainerW > 0)
        {
            try
            {
                var inv = items != null ? InventoryData.Read(items) : new InventoryData();
                Items = [.. inv.Items.Select(i => new ItemRow
                {
                    Name = PrefabCatalog.NameOf(i.Prefab) ?? i.Prefab.ToString(CultureInfo.InvariantCulture),
                    Stack = i.Stack,
                    Quality = i.Quality,
                    Durability = i.Durability,
                    X = i.X,
                    Y = i.Y,
                    Variant = i.Variant,
                    WorldLevel = i.WorldLevel,
                    CrafterId = i.CrafterId.ToString(CultureInfo.InvariantCulture),
                    CrafterName = i.CrafterName,
                    Equipped = i.Equipped,
                    PickedUp = i.PickedUp,
                    Cheated = i.Cheated,
                    CustomData = new(i.CustomData),
                })];
            }
            catch (Exception ex) when (CheckedReads.Damaged(ex))
            {
                Log.Caught(ex, "reading a container's items to inspect them");
                _inventoryError = ex.Message;
                Items = [];
            }
        }
        Stand = null;
        _armourStand = false;
        _standError = null;
        if (StandData.IsItemStand(z.Prefab))
        {
            var (held, way) = StandData.ReadItemStand(z);
            Stand = [new StandRow { Name = held?.Item ?? "", Quality = held?.Quality ?? 1, Variant = held?.Variant ?? 0 }];
            StandWay = way;
        }
        else if (PrefabCatalog.ArmourSlotsOf(z.Prefab) is { } slots)
        {
            _armourStand = true;
            var (held, pose) = StandData.ReadArmourStand(z, slots.Length);
            Stand = [];
            for (int i = 0; i < slots.Length; i++)
            {
                // ArmorStand has each of its slots twice: one row per kind, and any slot that holds something.
                int slot = i;
                if (held[i] == null && Enumerable.Range(0, i).Any(j => slots[j].SequenceEqual(slots[slot])))
                {
                    continue;
                }
                Stand.Add(new StandRow { Slot = i, Types = slots[i], Name = held[i]?.Item ?? "", Quality = held[i]?.Quality ?? 1, Variant = held[i]?.Variant ?? 0 });
            }
            StandWay = pose;
        }
        _standAtOpen = StandKey();
        _itemsAtOpen = ItemsKey();
        _slot = null;
        _moving = false;
        Render();
    }
}

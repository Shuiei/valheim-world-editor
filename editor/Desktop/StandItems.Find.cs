using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using static TerrainEditor.App.GameBundles;

namespace TerrainEditor.Desktop;

public static partial class StandItems
{
    /// <summary>
    /// The items the scene's stands hold, from each stand's data: an item stand's item (StandData.ReadItemStand) at
    /// its attach point, an armour stand's items (ReadArmourStand) at their slots' joints. A stand whose data cannot
    /// be read is logged and left out; an item without a model the stand can draw is left out.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="points">A stand prefab's points by name (null: not a stand the game has).</param>
    /// <param name="held">An item's held model by name and use (null: none).</param>
    internal static List<Held> Find(WorldScene s, Func<string, StandPoints?> points, Func<string, HeldUse, HeldModel?> held)
    {
        var list = new List<Held>();
        WorldScene.Thing[] things;
        lock (s.Things)
        {
            things = [.. s.Things];
        }
        foreach (var t in things)
        {
            bool item = StandData.IsItemStand(t.Prefab);
            if (t.Gone || !item && !StandData.IsArmourStand(t.Prefab) || PrefabCatalog.NameOf(t.Prefab) is not { } name || points(name) is not { } stand)
            {
                continue;
            }
            try
            {
                if (s.ObjectBytes(t) is { } bytes)
                {
                    var z = ZdoData.Parse(bytes);
                    var kind = ObjectKinds.Of(name, t.Piece || t.Joined, t.Tamed);
                    if (item)
                    {
                        OnItemStand(list, t, kind, stand, z, held);
                    }
                    else
                    {
                        OnArmourStand(list, t, kind, stand, z, held);
                    }
                }
            }
            catch (Exception e) when (CheckedReads.Damaged(e))
            {
                Log.CaughtBrief(e, "reading what a stand holds (its item is not drawn)");
            }
        }
        return list;
    }

    /// <summary>Adds an item stand's item, when it holds one the game can draw there.</summary>
    private static void OnItemStand(List<Held> list, WorldScene.Thing t, ObjectKind kind, StandPoints stand, ZdoData z, Func<string, HeldUse, HeldModel?> held)
    {
        var (h, orientation) = StandData.ReadItemStand(z);
        if (h != null && held(h.Item, HeldUse.Stand) is { } model && StandLook.OnItemStand(stand, model, h.Quality, orientation) is { } at)
        {
            list.Add(Place(t, kind, h.Item, HeldUse.Stand, at));
        }
    }

    /// <summary>Adds an armour stand's items, each at its slot's joint when the game draws it there.</summary>
    private static void OnArmourStand(List<Held> list, WorldScene.Thing t, ObjectKind kind, StandPoints stand, ZdoData z, Func<string, HeldUse, HeldModel?> held)
    {
        var (slots, _) = StandData.ReadArmourStand(z, stand.Slots?.Count ?? 0);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] is not { } h)
            {
                continue;
            }
            var use = StandLook.UseOfSlot(stand.Slots![i]);
            var itemKind = PrefabCatalog.ItemKindOf(StableHash.Of(h.Item));
            int type = itemKind == null ? 0 : itemKind.AttachOverride != 0 ? itemKind.AttachOverride : itemKind.Type;
            if (held(h.Item, use) is { } model && StandLook.OnArmourStand(stand, model, i, type) is { } at)
            {
                list.Add(Place(t, kind, h.Item, use, at));
            }
        }
    }

    /// <summary>An item drawn on a stand: the held model's key, the stand's placement and the item's place on it.</summary>
    private static Held Place(WorldScene.Thing t, ObjectKind kind, string item, HeldUse use, float[] at) =>
        new(StandLook.KeyOf(StandLook.ModelName(item, use)), t.Position, t.Rotation, t.Scale, BundleModels.ViewMatrix(at), kind);
}

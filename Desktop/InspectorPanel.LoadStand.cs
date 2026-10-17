using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// Reads the object's stand for the panel: an item stand's item, or an armour stand's slots and pose.
    /// </summary>
    /// <param name="z">The object's data.</param>
    private void LoadStand(ZdoData z)
    {
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
    }
}

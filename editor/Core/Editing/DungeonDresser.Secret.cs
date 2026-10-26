
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A hidden cache: two chests (whatever the Loot), treasure, bars, a brazier.</summary>
    private void Secret(Frame f)
    {
        Chest(f, -1.4f, f.HalfD - 1, 180, force: true);
        Chest(f, 1.4f, f.HalfD - 1, 180, force: true);
        Put(f, "treasure_pile", 0, 0.4f, R(0, 360), 1.1f);
        Put(f, _b.Bars, -2, -1, R(0, 360), 0.5f);
        Put(f, "Pickable_ForestCryptRemains01", 1.6f, -1.2f, R(0, 360), 0.5f);
        Pickables(f, 1.5f);
        Brazier(f, 2.4f, 1.2f);
    }
}

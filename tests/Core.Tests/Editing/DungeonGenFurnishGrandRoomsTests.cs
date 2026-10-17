using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated fortresses (DungeonGen): grand rooms get their own floors, with furniture standing on them.
/// </summary>
public class DungeonGenFurnishGrandRoomsTests
{
    [Fact]
    public void GrandRoomsGetTheirOwnFloorsWithFurnitureOn()
    {
        // A fortress with Rich furniture: over a few seeds, marble tiles in its halls and boards in its quarters.
        var rich = Enumerable.Range(1, 8).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 1, Seed: seed, Decor: 2)))
            .First(r => r.Items.Any(i => i.Prefab == "blackmarble_tile_floor_2x2") && r.Items.Any(i => i.Prefab == "wood_floor"));
        // Bare: the marble alone.
        var bare = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 1, Seed: 9, Decor: 0));
        Assert.DoesNotContain(bare.Items, i => i.Prefab is "blackmarble_tile_floor_2x2" or "wood_floor");
        // A chest (stone) on a wooden floor stands on the marble under it: not above the boards.
        var floors = rich.Items.Where(i => i.Prefab == "wood_floor").ToList();
        foreach (var chest in rich.Items.Where(i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal)))
        {
            // The boards under it (the highest at or below it, not a loft's deck overhead): one standing too high is
            // checked too.
            var under = floors.Where(f => MathF.Abs(f.Position.X - chest.Position.X) < 1 && MathF.Abs(f.Position.Z - chest.Position.Z) < 1 && f.Position.Y <= chest.Position.Y + 0.01f)
                .MaxBy(f => f.Position.Y);
            if (under != null)
            {
                Assert.InRange(chest.Position.Y - under.Position.Y, -0.01f, 0.06f);
            }
        }
    }
}

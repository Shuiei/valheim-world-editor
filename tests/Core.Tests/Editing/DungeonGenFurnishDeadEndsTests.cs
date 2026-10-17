using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated crypts (DungeonGen): dead-end corridors hold a cache and some rooms stand nearly bare.
/// </summary>
public class DungeonGenFurnishDeadEndsTests
{
    [Fact]
    public void DeadEndsHoldACacheAndSomeRoomsStandNearlyBare()
    {
        // Over a few crypts: some corridors lead nowhere (one doorway, no stairs) and each ends in the biome's chest;
        // and a few rooms of the ordinary kinds stand nearly bare: one piece, a light, little else.
        var crypts = Enumerable.Range(1, 16).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Crypt", Size: 3, Levels: 2, Seed: seed))).ToList();
        static IEnumerable<DungeonGen.Item> In(DungeonGen.Result r, DungeonGen.PlanRoom m) =>
            r.Items.Where(i => i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1 && i.Position.Y >= m.Y - 0.5f && i.Position.Y < m.Y + 11);
        var deadEnds = new List<(DungeonGen.Result R, DungeonGen.PlanRoom M)>();
        foreach (var r in crypts)
        {
            var map = r.Map!.ToList();
            for (int k = 0; k < map.Count; k++)
            {
                if (map[k].Corridor && r.Doors!.Count(d => d.A == k || d.B == k) == 1)
                {
                    deadEnds.Add((r, map[k]));
                }
            }
        }
        Assert.True(deadEnds.Count >= 3, $"{deadEnds.Count} dead ends");
        Assert.All(deadEnds, e => Assert.Contains(In(e.R, e.M), i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal)));
        // Nearly bare: of what is not the shell (walls, slabs, columns, beams, doors) nor light, cobweb or rubble, at most five things.
        static bool Furniture(DungeonGen.Item i) =>
            !i.Prefab.StartsWith("stone_wall", StringComparison.Ordinal) && !i.Prefab.StartsWith("blackmarble", StringComparison.Ordinal) && i.Prefab != "stone_pillar"
            && !i.Prefab.StartsWith("dungeon_", StringComparison.Ordinal) && !i.Prefab.Contains("torch", StringComparison.OrdinalIgnoreCase) && !i.Prefab.Contains("brazier", StringComparison.OrdinalIgnoreCase)
            && !i.Prefab.Contains("web", StringComparison.Ordinal) && !i.Prefab.Contains("Rubble", StringComparison.Ordinal) && !i.Prefab.StartsWith("Spawner_", StringComparison.Ordinal);
        var ordinary = crypts.SelectMany(r => r.Map!.Where(m => !m.Corridor && m.Name is "Great hall" or "Gallery" or "Burial chamber" or "Ossuary" or "Tomb").Select(m => In(r, m).Count(Furniture))).ToList();
        Assert.True(ordinary.Count(n => n <= 5) >= 2, $"bare rooms: {string.Join(",", ordinary.Order().Take(10))}");
        Assert.True(ordinary.Count(n => n > 8) >= ordinary.Count / 2, "most rooms are full");
    }
}

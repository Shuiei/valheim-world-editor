using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated keeps (DungeonGen): side rooms grow in wings of their kin, and deeper levels hold more loot.
/// </summary>
public class DungeonGenWingsTests
{
    [Fact]
    public void SideRoomsGrowInWingsOfTheirKinAndDeeperLevelsHoldMore()
    {
        // Over a few keeps: side rooms stand beside their kin (a kitchen by the feast hall, cells by the guard room)
        // far more often than chance would put them; and a level down holds more loot and foes than the one above.
        var keeps = Enumerable.Range(1, 12).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 2, Seed: seed))).ToList();
        var kinds = DungeonKit.Rooms.ToDictionary(r => r.Name, r => r.Room);
        int kin = 0, total = 0;
        foreach (var r in keeps)
        {
            var map = r.Map!.ToList();
            foreach (var d in r.Doors!.Where(d => d.Kind != "Stairs"))
            {
                var (a, b) = (map[d.A], map[d.B]);
                if (a.Corridor || b.Corridor || a.Main || b.Main)
                {
                    continue;
                }
                total++;
                if (DungeonPlanner.Kin(kinds[a.Name]).Contains(kinds[b.Name]) || DungeonPlanner.Kin(kinds[b.Name]).Contains(kinds[a.Name]))
                {
                    kin++;
                }
            }
        }
        // Loops and twins join rooms as they lie; the wings still make kin of a good third of these doorways.
        Assert.True(total >= 10 && kin * 3 >= total, $"{kin} of {total} side-to-side doorways join kin");
        // Things on the level's own floor (or a loft over it): below the floor of the level above, 6 m up, so a room
        // under another level's does not count that level's things.
        static float PerRoom(IEnumerable<DungeonGen.Result> rs, int level, Func<DungeonGen.Item, bool> what) =>
            (float)rs.Sum(r => r.Items.Count(i => what(i) && r.Map!.Any(m => m.Level == level && i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1 && i.Position.Y >= m.Y - 0.5f && i.Position.Y < m.Y + 5.5f)))
            / Math.Max(1, rs.Sum(r => r.Map!.Count(m => m.Level == level && !m.Corridor)));
        Assert.True(PerRoom(keeps, 1, i => i.Prefab.StartsWith("Pickable_", StringComparison.Ordinal)) > PerRoom(keeps, 0, i => i.Prefab.StartsWith("Pickable_", StringComparison.Ordinal)), "deeper levels hold more loot");
    }
}

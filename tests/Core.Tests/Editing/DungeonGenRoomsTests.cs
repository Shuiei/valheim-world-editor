using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) and their rooms: every biome's own rooms, the game's rooms as a dungeon of their own, the
/// kind with no rooms to join made a cave, the loot and foes the settings choose, and side rooms that stand beside their kin.
/// </summary>
public class DungeonGenRoomsTests
{
    [Theory]
    [InlineData("Meadows")]
    [InlineData("Black Forest")]
    [InlineData("Swamp")]
    [InlineData("Mountain")]
    [InlineData("Plains")]
    [InlineData("Mistlands")]
    [InlineData("Ashlands")]
    [InlineData("Deep North")]
    public void EveryBiomesOwnRoomsCanBeGenerated(string biome)
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: biome, Size: 2, Seed: 5));
        Assert.True(r.Rooms!.Count > 3);
    }

    [Fact]
    public void TheGamesRoomsCanBeGeneratedToo()
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 2, Seed: 3));
        Assert.NotNull(r.Kind);
        Assert.True(r.Rooms!.Count > 5);
        Assert.Empty(Dungeons.Openings(r.Rooms, freeOnly: true));
    }

    [Fact]
    public void AKindWithNoRoomsToJoinIsMadeACave()
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Kind: "DG_GoblinCamp", Size: 2, Seed: 3));
        Assert.Equal("DG_Cave", r.Kind!.Name);
        Assert.Contains(r.Notes, n => n.English.Contains("a cave instead", StringComparison.Ordinal));
    }

    [Fact]
    public void TheGamesRoomsLootAndFoesFollowTheSettings()
    {
        static DungeonGen.Result Cave(Func<DungeonGen.Settings, DungeonGen.Settings> change) =>
            DungeonGen.Make(change(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 3, Seed: 3)));
        static int Chests(DungeonGen.Result r) => r.Items.Count(i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal));
        int chests = Chests(Cave(s => s));
        Assert.True(chests > 0);
        // No loot: every chest taken; twice the loot: a second chest beside each.
        Assert.Equal(0, Chests(Cave(s => s with { Loot = 0 })));
        Assert.Equal(2 * chests, Chests(Cave(s => s with { Loot = 2 })));
        // Chosen foes in the rooms' spawners' places.
        var spawners = Cave(s => s with { Foes = ["Spawner_MadeUp"] }).Items.Where(i => i.Prefab.StartsWith("Spawner_", StringComparison.Ordinal)).Select(i => i.Prefab).Distinct();
        Assert.Equal(["Spawner_MadeUp"], spawners);
    }

    [Fact]
    public void TheGamesRoomsAreLaidOutFromADungeonObjectWhenOneIsGiven()
    {
        // A Frost Cave from its dungeon object at the zone's middle, as the game lays its own out from the cave's
        // entrance: the first room's way in at the object, the rooms in world metres, in the zone's box.
        var pose = new DungeonEntrances.Pose(new Vector3(0, 5165.023f, -2654), Quaternion.Identity);
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 2, Seed: 3), 0, pose);
        Assert.Equal(pose, r.Dungeon);
        var first = r.Rooms![0];
        Assert.True(first.Room!.Entrance);
        Assert.True(Vector3.Distance(pose.Position, first.At(first.Room.Openings.First(o => o.Entrance)).Position) < 0.01f);
        Assert.All(r.Rooms, p => Assert.True(Dungeons.Inside(r.Kind!, pose.Position, p), p.Name));
        Assert.All(r.Items, i => Assert.True(i.Position.Y > 5000));
        Assert.True(Vector3.Distance(r.Arrival, pose.Position) < 20, r.Arrival.ToString());
        // From the origin: no dungeon object, the first room there, facing +z.
        var plain = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 2, Seed: 3));
        Assert.Null(plain.Dungeon);
        Assert.Equal(Vector3.Zero, plain.Rooms![0].Position);
    }

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

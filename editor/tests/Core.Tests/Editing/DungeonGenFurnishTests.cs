using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) furnished room by room: the shape of rooms within their boxes and rooms of a kind
/// furnished in several ways.
/// </summary>
public class DungeonGenFurnishTests
{
    [Fact]
    public void RoomsTakeShapesWithinTheirBoxesAndCorridorsBend()
    {
        // Over a few dungeons: some rooms are not their whole box (notched, an apse, an L, a neck), each part within
        // the box; some corridors bend (two parts); every doorway still lands on a wall (checked above for the samples).
        var made = Enumerable.Range(1, 6).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Catacombs", Size: 3, Levels: 1, Seed: seed))).ToList();
        var shaped = made.SelectMany(r => r.Map!).Where(m => !m.Corridor && m.Parts != null).ToList();
        Assert.True(shaped.Count >= 5, $"{shaped.Count} shaped rooms");
        Assert.All(shaped, m => Assert.All(m.Parts!, p => Assert.True(p.X0 >= m.X0 && p.X1 <= m.X1 && p.Z0 >= m.Z0 && p.Z1 <= m.Z1 && p.X1 > p.X0 && p.Z1 > p.Z0)));
        // A shaped room's floor is less than its box, never empty.
        Assert.All(shaped, m => Assert.InRange(m.Parts!.Sum(p => (p.X1 - p.X0) * (p.Z1 - p.Z0)), 16f, (m.X1 - m.X0) * (m.Z1 - m.Z0) - 3.9f));
        var bent = made.SelectMany(r => r.Map!).Where(m => m.Corridor && m.Parts is { Count: > 1 }).ToList();
        Assert.True(bent.Count >= 2, $"{bent.Count} bent corridors");
        // A room without parts floors its whole box.
        var whole = made.SelectMany(r => r.Map!).First(m => !m.Corridor && m.Parts == null);
        Assert.Equal([(whole.X0, whole.Z0, whole.X1, whole.Z1)], whole.Floor);
    }

    [Fact]
    public void RoomsOfAKindAreFurnishedInSeveralWays()
    {
        // The same crypt from different seeds: chapels with pews in some, a raised altar in others; burial chambers
        // in rows or on a dais; walls lit by sconces in some rooms, standing torches in others.
        var made = Enumerable.Range(1, 12).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Crypt", Size: 3, Levels: 2, Seed: seed))).ToList();
        HashSet<string> All(Func<DungeonGen.Result, bool> which) => [.. made.Where(which).SelectMany(r => r.Items.Select(i => i.Prefab))];
        var prefabs = All(_ => true);
        Assert.Contains("piece_bench01", prefabs);
        Assert.Contains("stone_floor_2x2", prefabs);
        Assert.Contains("stone_stair", prefabs);
        Assert.Contains("piece_walltorch", prefabs);
        Assert.Contains("CastleKit_groundtorch_green", prefabs);
        Assert.Contains("morkhalla_web_corner", prefabs);
        // Chapels: some with pews, some without; burial chambers: some with a dais, some without.
        static bool Holds(DungeonGen.Result r, DungeonGen.PlanRoom m, string prefab) =>
            r.Items.Any(i => i.Prefab == prefab && i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1 && MathF.Abs(i.Position.Y - m.Y) < 3);
        var chapels = made.SelectMany(r => r.Map!.Where(m => m.Name == "Chapel").Select(m => Holds(r, m, "piece_bench01"))).ToList();
        Assert.Contains(true, chapels);
        Assert.Contains(false, chapels);
        var burials = made.SelectMany(r => r.Map!.Where(m => m.Name == "Burial chamber").Select(m => Holds(r, m, "stone_floor_2x2"))).ToList();
        Assert.Contains(true, burials);
        Assert.Contains(false, burials);
    }
}

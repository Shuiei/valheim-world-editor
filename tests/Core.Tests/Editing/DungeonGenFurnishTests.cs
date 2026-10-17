using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) furnished room by room: the shape of rooms within their boxes, rooms of a kind furnished in
/// several ways, grand rooms on their own floors, alcoves and niches furnished, tall rooms with lofts and pits, and the dead ends and
/// bare rooms.
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

    [Fact]
    public void AlcovesAndNichesAreFurnished()
    {
        // Over a few crypts and catacombs: rooms with alcoves (two tiles wide) and corridors with niches (one), each
        // with something standing in it, well inside (nothing in the room's walls).
        var made = Enumerable.Range(1, 10).SelectMany(seed => new[] { "Crypt", "Catacombs" }.Select(style =>
            DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: seed)))).ToList();
        var alcoves = made.SelectMany(r => r.Map!.Where(m => !m.Corridor && m.Nooks != null).SelectMany(m => m.Nooks!.Select(n => (r, m, n)))).ToList();
        var niches = made.SelectMany(r => r.Map!.Where(m => m.Corridor && m.Nooks != null).SelectMany(m => m.Nooks!.Select(n => (r, m, n)))).ToList();
        Assert.True(alcoves.Count >= 4, $"{alcoves.Count} alcoves");
        Assert.True(niches.Count >= 4, $"{niches.Count} niches");
        Assert.All(alcoves, a => Assert.True(MathF.Max(a.n.X1 - a.n.X0, a.n.Z1 - a.n.Z0) == 4 && MathF.Min(a.n.X1 - a.n.X0, a.n.Z1 - a.n.Z0) == 2, "an alcove is two tiles along the wall"));
        Assert.All(niches, a => Assert.True(a.n.X1 - a.n.X0 == 2 && a.n.Z1 - a.n.Z0 == 2, "a niche is one tile"));
        static bool Furnished((DungeonGen.Result R, DungeonGen.PlanRoom M, (float X0, float Z0, float X1, float Z1) N) a) =>
            a.R.Items.Any(i => !i.Prefab.StartsWith("stone_wall", StringComparison.Ordinal) && !i.Prefab.Contains("web", StringComparison.Ordinal)
                && i.Position.X > a.N.X0 + 0.2f && i.Position.X < a.N.X1 - 0.2f && i.Position.Z > a.N.Z0 + 0.2f && i.Position.Z < a.N.Z1 - 0.2f
                && i.Position.Y > a.M.Y - 0.5f && i.Position.Y < a.M.Y + 1.5f);
        Assert.True(alcoves.Count(Furnished) >= alcoves.Count * 3 / 4, $"{alcoves.Count(Furnished)} of {alcoves.Count} alcoves furnished");
        Assert.True(niches.Count(Furnished) >= niches.Count * 3 / 4, $"{niches.Count(Furnished)} of {niches.Count} niches furnished");
    }

    [Fact]
    public void TallRoomsGetLoftsAndRoomsGetPitsVaultsAndGatedCorridors()
    {
        // Over a few keeps and crypts: a loft over the way in of some great hall or throne room (its rail and the top
        // of its stairs 3 m up), a sunken shrine or arena (ledges a metre up round the room), a burial vault (walls of
        // the material well inside the room's box), a corridor closed by a gate midway, bones along another.
        // Lofts are rare (a tall room 12 m deep with free sides, half the time): 24 seeds of each.
        var keeps = Enumerable.Range(1, 24).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 1, Seed: seed))).ToList();
        var crypts = Enumerable.Range(1, 24).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Crypt", Size: 3, Levels: 2, Seed: seed))).ToList();
        static IEnumerable<DungeonGen.Item> In(DungeonGen.Result r, DungeonGen.PlanRoom m, float inset = 0) =>
            r.Items.Where(i => i.Position.X > m.X0 + inset && i.Position.X < m.X1 - inset && i.Position.Z > m.Z0 + inset && i.Position.Z < m.Z1 - inset && i.Position.Y >= m.Y - 0.5f && i.Position.Y < m.Y + 11);
        var lofts = keeps.Concat(crypts).SelectMany(r => r.Map!.Where(m => m.Name is "Great hall" or "Throne room" or "Chapel").Select(m => In(r, m).ToList()))
            .Where(items => items.Any(i => i.Prefab == "stone_fence") && items.Any(i => i.Prefab == "stone_stair" && MathF.Abs(i.Position.Y % 6 - 3.05f) < 0.1f || i.Prefab == "stone_stair" && MathF.Abs(i.Position.Y % 6 + 6 - 3.05f) < 0.1f)).ToList();
        Assert.True(lofts.Count >= 2, $"{lofts.Count} lofts");
        // A loft's rail runs the room's width less the stairs' tile: at least 3 pieces of fence, 4 m up.
        Assert.All(lofts, items => Assert.True(items.Count(i => i.Prefab == "stone_fence") >= 3));
        var pits = crypts.SelectMany(r => r.Map!.Where(m => m.Name is "Shrine" or "Arena").Select(m => In(r, m).Count(i => i.Prefab == "stone_floor_2x2" && MathF.Abs(i.Position.Y - m.Y - 0.55f) < 0.05f))).ToList();
        Assert.True(pits.Any(n => n >= 8), $"ledges: {string.Join(",", pits)}");
        var vaults = crypts.SelectMany(r => r.Map!.Where(m => m.Name == "Burial chamber").Select(m => In(r, m, 1.2f).Count(i => i.Prefab.StartsWith("stone_wall_", StringComparison.Ordinal) && !i.Prefab.EndsWith("_ruin", StringComparison.Ordinal)))).ToList();
        Assert.True(vaults.Any(n => n >= 4), $"vault walls: {string.Join(",", vaults)}");
        var corridors = crypts.SelectMany(r => r.Map!.Where(m => m.Corridor).Select(m => In(r, m).Select(i => i.Prefab).ToList())).ToList();
        Assert.True(corridors.Any(p => p.Any(n => n.StartsWith("dungeon_sunkencrypt_irongate", StringComparison.Ordinal))), "no gated corridor");
        Assert.True(corridors.Any(p => p.Count(n => n is "skull_pile" or "bone_stack") >= 4), $"bones: {string.Join(",", corridors.Select(p => p.Count(n => n is "skull_pile" or "bone_stack")))}");
    }

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

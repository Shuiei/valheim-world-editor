using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated keeps and crypts (DungeonGen): tall rooms get lofts, rooms get pits, vaults and gated corridors.
/// </summary>
public class DungeonGenFurnishLoftsTests
{
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
}

using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated crypts and catacombs (DungeonGen): alcoves and niches are furnished.
/// </summary>
public class DungeonGenFurnishAlcovesTests
{
    private static readonly string[] Expected1 = ["Crypt", "Catacombs"];
    [Fact]
    public void AlcovesAndNichesAreFurnished()
    {
        // Over a few crypts and catacombs: rooms with alcoves (two tiles wide) and corridors with niches (one), each
        // with something standing in it, well inside (nothing in the room's walls).
        var made = Enumerable.Range(1, 10).SelectMany(seed => Expected1.Select(style =>
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
}

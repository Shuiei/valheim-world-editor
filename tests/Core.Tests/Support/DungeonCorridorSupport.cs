using TerrainEditor.Editing;

namespace WorldEditor.Tests;

/// <summary>
/// Dresses the corridors of generated dungeons as one kind, for the corridor tests (each corridor at least four
/// metres across, the width the kinds are laid out for).
/// </summary>
internal static class DungeonCorridorSupport
{
    /// <summary>The objects a kind of corridor dressing puts in the corridors of dungeons made from these seeds.</summary>
    public static List<DungeonGen.Item> Dress(string kind, params int[] seeds)
    {
        var biome = DungeonKit.BiomeOf("Black Forest");
        var mat = DungeonKit.MaterialOf(biome.Material);
        var style = DungeonKit.StyleOf("Crypt");
        var o = new DungeonOut();
        foreach (int seed in seeds)
        {
            var s = new DungeonGen.Settings(Biome: "Black Forest", Style: style.Name, Size: 3, Levels: 1, Seed: seed);
            var plan = DungeonPlanner.Make(s, style, new Random(seed));
            var frames = plan.Spaces.Where(sp => sp.Corridor && sp.Shape == DungeonPlan.Shape.Rect)
                .Select(sp => new DungeonDresser.Frame(sp, mat, plan.Levels[sp.Level])).Where(f => f.HalfD >= 4);
            foreach (var f in frames)
            {
                DungeonDresser.DressCorridor(f, kind, mat, biome, style, s, new Random(seed), o);
            }
        }
        return o.Items;
    }
}

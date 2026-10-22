using ValheimGen;
using WorldEditor.Tests;
using Xunit;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// Expand World Data's default world rules (ExpandWorldMod.DefaultWorld, its world file when it has none) followed by
/// the editor's generator: the game's own layout in the mod's words.
/// </summary>
public class ExpandWorldModDefaultWorldTests
{
    private static WorldGenerator Generator(WorldRules? rules = null) =>
        WorldGenerator.Create(new World { Seed = 12345, WorldGenVersion = 2, Rules = rules ?? WorldRules.Vanilla });

    [Fact]
    public void TheModsDefaultRulesGiveTheGamesLayout()
    {
        // The mod's default rules say the game's own layout in its words (the same circles, waves, bands, noise
        // fields and thresholds, in the same order): the biomes come out the same, but for a point on an exact edge.
        var (rules, sectors) = ExpandWorldMod.WorldOf(ExpandWorldMod.DefaultWorld, BiomeTable.Vanilla, 10000f, ExpandWorldMod.Wiggles.Default, []);
        var g = Generator(new WorldRules { DataInstalled = true, World = rules, CheckSectors = sectors });
        var plain = Generator();
        Assert.False(sectors);
        var points = GeneratorPoints.Rings(200, 9800, 400, 24).ToList();
        Assert.All(points, p => Assert.Equal(plain.GetBiome(p.X, p.Z), g.GetBiome(p.X, p.Z)));
        Assert.Contains(points, p => g.GetBiome(p.X, p.Z) == Heightmap.Biome.Mountain);
        Assert.Contains(points, p => g.GetBiome(p.X, p.Z) == Heightmap.Biome.Swamp);
        // The Ashlands' rule: 12 000 to 16 000 m from a middle 4 000 m north, first in the list.
        Assert.Equal(Heightmap.Biome.AshLands, g.GetBiome(0f, -8500f));
        Assert.True(g.IsAshlands(0f, -8500f));
    }
}

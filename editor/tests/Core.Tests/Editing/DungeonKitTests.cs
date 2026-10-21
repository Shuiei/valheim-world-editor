using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The biomes' palettes: a spawner stands for one creature, so an elite that comes back is the elite it names.
/// </summary>
public class DungeonKitTests
{
    [Fact]
    public void ASpawnerStandsForOneCreature()
    {
        // The Black Forest's elite was Spawner_Skeleton with Skeleton_NoArcher: with Respawn on, an ordinary skeleton
        // (archers too) came back; with it off, a sword skeleton stood.
        var pairs = DungeonKit.Biomes.SelectMany(b => b.Foes).ToList();
        foreach (var b in DungeonKit.Biomes)
        {
            foreach (var e in b.Elites)
            {
                Assert.All(pairs.Where(f => f.Spawner == e.Spawner), f => Assert.Equal(e.Creature, f.Creature));
            }
        }
    }
}

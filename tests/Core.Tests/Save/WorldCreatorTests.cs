using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A new world's ground: the seed's own ground and the seed number, and the same seed making the same ground on several
/// threads at once.
/// </summary>
[Collection("World files")]
public sealed class WorldCreatorTests : WorldCreatorTestBase
{
    [Fact]
    public void TheSeedNumberIsTheGames()
    {
        Assert.Equal(-1261093776, WorldCreator.SeedOf(Seed));
        Assert.Equal(WorldSave.Load(Path.Combine(Fixtures.Root, "CITest")).Seed, WorldCreator.SeedOf(Seed));
    }

    [Fact]
    public void TheGroundIsTheSeedsOwn()
    {
        WorldCreator.Create(Folder("Ground"), "Ground", Seed);
        float[] made = new ValheimGen.TerrainService(WorldSave.Load(Folder("Ground"))).RawZone(5, -4);
        float[] game = new ValheimGen.TerrainService(WorldSave.Load(Path.Combine(Fixtures.Root, "CITest"))).RawZone(5, -4);
        Assert.Equal(game, made);
        // Another seed, other ground.
        WorldCreator.Create(Folder("Other"), "Other", "AnotherOne");
        Assert.NotEqual(game, new ValheimGen.TerrainService(WorldSave.Load(Folder("Other"))).RawZone(5, -4));
    }

    [Fact]
    public void WorldsMadeOnSeveralThreadsAtOnceHaveTheirOwnGround()
    {
        WorldCreator.Create(Folder("One"), "One", Seed);
        WorldCreator.Create(Folder("Two"), "Two", "AnotherOne");
        var saves = new[] { WorldSave.Load(Folder("One")), WorldSave.Load(Folder("Two")) };
        var zones = new[] { (0, 0), (5, -4), (-3, 2), (2, 3) };
        float[][] Ground(WorldSave w)
        {
            var t = new ValheimGen.TerrainService(w);
            return [.. zones.Select(z => t.RawZone(z.Item1, z.Item2).Concat(Enumerable.Range(0, 9).Select(k => (float)t.BiomeAt(z.Item1 * 64 + k * 7, z.Item2 * 64 - k * 7))).ToArray())];
        }
        float[][][] alone = [.. saves.Select(Ground)];
        // Each thread makes its world's generator again and again while the others make theirs.
        var together = Enumerable.Range(0, 8).AsParallel().WithDegreeOfParallelism(8)
            .Select(i => (i % 2, Enumerable.Range(0, 3).Select(_ => Ground(saves[i % 2])).ToList())).ToList();
        foreach (var (seed, runs) in together)
        {
            Assert.All(runs, r => Assert.Equal(alone[seed], r));
        }
    }
}

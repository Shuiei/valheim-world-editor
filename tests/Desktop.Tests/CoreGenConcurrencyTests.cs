using TerrainEditor.Save;
using Xunit;
using TerrainService = ValheimGen.TerrainService;

namespace WorldEditor.Tests;

// The world generator used by several worlds and threads at once: a second world's generator leaves the
// first one's rivers alone (they were cleared: a map still drawing the first world lost them), the map
// data reads its own world's generator, and generators made at the same time (each seeding Unity's one
// Random) all come out the same.
[Collection("World files")]
public class CoreGenConcurrencyTests
{
	// The ground of 8 × 8 zones around 0, 0 (rivers and streams run through some), summed per zone.
	private static double[] Ground(TerrainService t) =>
		Enumerable.Range(0, 64).Select(i => t.RawZone(i % 8 - 4, i / 8 - 4).Sum(h => (double)h)).ToArray();

	[Fact]
	public void AnotherWorldsGeneratorLeavesThisOnesRivers()
	{
		using var w = new TempWorld();
		WorldSave save = w.Load();
		var first = new TerrainService(save);
		var before = Ground(new TerrainService(save));
		var second = new TerrainService(save);
		Assert.NotSame(first.Generator, second.Generator);
		// The first one's zones, made after the second generator: as before (no river taken away).
		Assert.Equal(before, Ground(first));
		Assert.Same(first.Generator, new ValheimGen.MapData(first, new TerrainEditor.Editing.EditStore(save)).GeneratorForTests);
	}

	[Fact]
	public void GeneratorsMadeAtOnceAreAlike()
	{
		using var w = new TempWorld();
		WorldSave save = w.Load();
		var reference = Ground(new TerrainService(save));
		var made = new TerrainService[8];
		Parallel.For(0, made.Length, i => made[i] = new TerrainService(save));
		Assert.All(made, t => Assert.Equal(reference, Ground(t)));
	}
}

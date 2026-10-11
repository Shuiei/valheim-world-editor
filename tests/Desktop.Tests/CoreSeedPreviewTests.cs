using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

// A seed seen before its world exists (SeedPreview): the game's generator sampled over the whole
// world, the land, the biomes, the likely start and how far each biome is from it; scores that follow
// the wishes; a search that keeps the best seeds.
public class CoreSeedPreviewTests
{
	[Fact]
	public void ASeedsPreviewMeasuresItsWorld()
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		var p = SeedPreview.Make("abc", 128, cancel: TestContext.Current.CancellationToken);
		var took = watch.Elapsed;
		Assert.Equal(128 * 128, p.Biome.Length);
		var s = p.Stats;
		// Valheim's worlds: about a third land (the rest ocean), every biome present.
		Assert.InRange(s.Land, 0.15f, 0.7f);
		Assert.Equal(1, s.Shares.Values.Sum(), 2);
		Assert.All(new[] { Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain, Heightmap.Biome.Plains }, b => Assert.True(s.Shares[b] > 0, b.ToString()));
		// The start: the game's start temple, in Meadows near the middle.
		Assert.True(MathF.Sqrt(s.Start.X * s.Start.X + s.Start.Z * s.Start.Z) < 1500, $"start at {s.Start}");
		Assert.Equal(Heightmap.Biome.Meadows, SeedPreview.Generator("abc").GetBiome(s.Start.X, s.Start.Z));
		Assert.True(s.Distance[Heightmap.Biome.Meadows] < p.Cell, $"Meadows at {s.Distance[Heightmap.Biome.Meadows]} m");
		Assert.Null(s.Nearest);
		Assert.Null(p.Landmarks);
		Assert.True(s.Distance[Heightmap.Biome.BlackForest] < s.Distance[Heightmap.Biome.DeepNorth]);
		Assert.InRange(s.StartContinent, 0.0001f, 1);
		Assert.True(took < TimeSpan.FromSeconds(20), $"took {took}");
		// The same seed, the same world.
		Assert.Equal(p.Biome, SeedPreview.Make("abc", 128, cancel: TestContext.Current.CancellationToken).Biome);
		Assert.NotEqual(p.Biome, SeedPreview.Make("abd", 128, cancel: TestContext.Current.CancellationToken).Biome);
	}

	[Fact]
	public void APreviewCanShowTheBossesAndTraders()
	{
		var p = SeedPreview.Make("5DCcdIcuYJ", 64, parallel: true, landmarks: true, cancel: TestContext.Current.CancellationToken);
		var start = Assert.Single(p.Landmarks!, m => m.Kind == "start");
		Assert.Equal((start.X, start.Z), p.Stats.Start);
		Assert.Equal(3, p.Landmarks!.Count(m => m.Name == "Eikthyr"));
		Assert.All(p.Landmarks!.Where(m => m.Kind == "trader"), m => Assert.True(m.OneOf));
		Assert.All(p.Landmarks!.Where(m => m.Kind == "boss"), m => Assert.False(m.OneOf));
		// The nearest Eikthyr altar (110, 206 from the temple at 0.4, -6.9): about 239 m.
		Assert.InRange(p.Stats.Nearest!["Eikthyr"], 235, 245);
		Assert.True(p.Stats.Nearest["Haldor"] >= 1000, "Haldor keeps away from the middle");
		var wishes = new SeedPreview.Wishes(MaxHaldorDistance: 3000);
		Assert.True(wishes.NeedsLandmarks);
		Assert.False(new SeedPreview.Wishes().NeedsLandmarks);
		Assert.True(SeedPreview.Score(p.Stats, wishes) != SeedPreview.Score(p.Stats, wishes with { MaxHaldorDistance = 0 }));
	}

	[Fact]
	public void ScoresFollowTheWishesAndTheSearchKeepsTheBest()
	{
		var near = SeedPreview.Biomes.ToDictionary(b => b, _ => 500f);
		var far = SeedPreview.Biomes.ToDictionary(b => b, _ => 5000f);
		var shares = SeedPreview.Biomes.ToDictionary(b => b, _ => 0.1f);
		var same = SeedPreview.Biomes.ToDictionary(b => b, _ => true);
		var close = new SeedPreview.Stats(0.4f, shares, 0.6f, (0, 0), 1, 0.6f, near, same);
		var distant = close with { Distance = far, LandNearStart = 0.3f, StartContinent = 0.05f };
		var wishes = new SeedPreview.Wishes(MaxSwampDistance: 1500, MaxMountainDistance: 2000);
		Assert.True(SeedPreview.Score(close, wishes) > SeedPreview.Score(distant, wishes));
		Assert.True(SeedPreview.Score(close with { Land = 0.2f }, wishes with { MinLand = 0.35f }) < SeedPreview.Score(close, wishes with { MinLand = 0.35f }));
		Assert.Equal(10, SeedPreview.RandomSeed().Length);
		int told = 0;
		var found = SeedPreview.Search(wishes, 6, 3, size: 48, progress: (done, total) => Interlocked.Exchange(ref told, done), cancel: TestContext.Current.CancellationToken);
		Assert.Equal(3, found.Count);
		Assert.Equal(6, told);
		Assert.True(found[0].Score >= found[1].Score && found[1].Score >= found[2].Score);
		// Stopped at once: what was found so far (nothing), no error.
		Assert.Empty(SeedPreview.Search(wishes, 50, 3, cancel: new CancellationToken(true)));
	}
}

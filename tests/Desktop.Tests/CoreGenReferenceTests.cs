using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

// The ported world generator against values recorded in the real game (tests/fixtures/
// worldgen-reference.txt, written by the TerrainDump client plugin for seed 5DCcdIcuYJ, world
// generator version 2): Unity's random numbers and Perlin noise, 3000 heights and biomes, eight whole
// zones and the count of lakes, rivers and streams, every value compared bit for bit.
[Collection("World files")]
public class CoreGenReferenceTests
{
	private static string Reference => Path.Combine(Fixtures.Root, "worldgen-reference.txt");

	// Where the port stands: the random numbers, the noise, every biome and the water are exact; all
	// but 18 of the 3000 heights and 6 of the 8 zones are bit for bit, and the rest differ by less than
	// a millimetre (floating point done in a different order than the game's runtime). Any change that
	// moves further from the game fails here.
	[Fact]
	public void TheGeneratorMatchesTheGame()
	{
		var r = DumpVerifier.Run(Reference, "5DCcdIcuYJ");
		string why = $"{r with { Errors = new() }}\n" + string.Join("\n", r.Errors);
		Assert.True(r.RandomOk == 57 && r.RandomWrong == 0, why);
		Assert.True(r.PerlinOk == 1207 && r.PerlinWrong == 0, why);
		Assert.True(r.BiomesWrong == 0 && r.WaterWrong == 0, why);
		Assert.True(r.HeightsOk >= 2982 && r.HeightsOk + r.HeightsWrong == 3000, why);
		Assert.True(r.ZonesOk >= 6 && r.ZonesOk + r.ZonesWrong == 8, why);
		Assert.True(r.HeightMaxDiff < 0.001, why);
		Assert.True(r.ZoneMaxDiff < 0.001, why);
	}

	[Fact]
	public void TheSeedNameGivesTheGamesSeed() => Assert.Equal(509094289, TerrainEditor.Save.StableHash.Of("5DCcdIcuYJ"));

	[Fact]
	public void AnotherSeedDoesNotMatch()
	{
		// The same records under another seed: the random numbers and noise still match (they do not
		// depend on the world), the heights, biomes, zones and water mostly do not.
		var r = DumpVerifier.Run(Reference, "SomethingElse");
		Assert.Equal(57, r.RandomOk);
		Assert.Equal(1207, r.PerlinOk);
		// A few places do not depend on the seed (beyond the world's edge): most do.
		Assert.True(r.HeightsWrong > 2800, $"{r with { Errors = new() }}");
		Assert.True(r.BiomesWrong > 1000, $"{r with { Errors = new() }}");
		Assert.Equal(8, r.ZonesWrong);
		Assert.Equal(1, r.WaterWrong);
		Assert.Equal(25, r.Errors.Count);
		Assert.False(r.AllMatch);
	}

	[Fact]
	public void BrokenRandomRecordsAreReported()
	{
		string bad = Path.Combine(Path.GetTempPath(), "vwe-ref-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
		try
		{
			// A value that does not follow from the state, and a Perlin value off by one bit.
			File.WriteAllLines(bad, new[]
			{
				"R init 1 | 00000001 6c078966 714acb3f dbffe6dc",
				"R value 3f000000 | 6c078966 714acb3f dbffe6dc dbfff5aa",
				File.ReadLines(Reference).First(l => l.StartsWith("P ")) is var p ? p[..^1] + (p[^1] == '0' ? '1' : '0') : "",
			});
			var r = DumpVerifier.Run(bad, "5DCcdIcuYJ");
			Assert.Equal(1, r.RandomOk);
			Assert.Equal(1, r.RandomWrong);
			Assert.Equal(1, r.PerlinWrong);
			Assert.Contains(r.Errors, e => e.StartsWith("Random: expected [value 3f000000"));
			Assert.Contains(r.Errors, e => e.StartsWith("Perlin("));
		}
		finally
		{
			File.Delete(bad);
		}
	}
}

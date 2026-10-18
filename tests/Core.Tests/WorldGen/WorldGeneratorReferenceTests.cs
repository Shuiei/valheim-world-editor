using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ported world generator against values recorded in the real game (tests/fixtures/ worldgen-reference.txt, written
/// by the TerrainDump client plugin for seed 5DCcdIcuYJ, world generator version 2): Unity's random numbers and Perlin
/// noise, 3000 heights and biomes, eight whole zones and the count of lakes, rivers and streams, every value compared
/// bit for bit.
/// </summary>
[Collection("World files")]
public class WorldGeneratorReferenceTests
{
    private static string Reference => Path.Combine(Fixtures.Root, "worldgen-reference.txt");

    // Where the port stands: the random numbers, the noise, every biome, the water and the 8 zones are
    // exact; all but 8 of the 3000 heights are bit for bit (all in the Deep North), and those differ by
    // less than a millimetre (floating point done in a different order than the game's runtime). Any
    // change that moves further from the game fails here.
    [Fact]
    public void TheGeneratorMatchesTheGame()
    {
        var r = DumpVerifier.Run(Reference, "5DCcdIcuYJ");
        string why = $"{r with { Errors = [] }}\n" + string.Join("\n", r.Errors);
        Assert.True(r.RandomOk == 57 && r.RandomWrong == 0, why);
        Assert.True(r.PerlinOk == 1207 && r.PerlinWrong == 0, why);
        Assert.True(r.BiomesWrong == 0 && r.WaterWrong == 0, why);
        Assert.True(r.HeightsOk == 2992 && r.HeightsOk + r.HeightsWrong == 3000, why);
        Assert.True(r.ZonesOk == 8 && r.ZonesOk + r.ZonesWrong == 8, why);
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
        Assert.True(r.HeightsWrong > 2800, $"{r with { Errors = [] }}");
        Assert.True(r.BiomesWrong > 1000, $"{r with { Errors = [] }}");
        Assert.Equal(8, r.ZonesWrong);
        Assert.Equal(1, r.WaterWrong);
        Assert.Equal(25, r.Errors.Count);
        Assert.False(r.AllMatch);
    }

    [Fact]
    public void UnknownAndShortLinesAreReportedAndLeaveTheRandomStateAlone()
    {
        // An unknown Random call was replayed as insideUnitCircle (moving the state on, so every later line differed),
        // and a short line threw. A line of no kind the verifier compares (a note, another kind) is skipped: neither
        // unreadable nor counted.
        string bad = Path.Combine(Path.GetTempPath(), "vwe-ref-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        try
        {
            // The game's first two calls (a seed, then a range), an unknown call between them.
            string[] game = [.. File.ReadLines(Reference).Where(l => l.StartsWith("R ", StringComparison.Ordinal)).Take(2)];
            File.WriteAllLines(bad,
            [
                game[0],
                "R gaussian 3f000000 | 6c078966 714acb3f dbffe6dc dbfff5aa",
                game[1],
                "P 3f000000",
                "H 1",
                "R value",
                "# a note",
                "Q 1 2 3",
            ]);
            var r = DumpVerifier.Run(bad, "5DCcdIcuYJ");
            Assert.Equal(4, r.Unreadable);
            Assert.Equal(0, r.RandomWrong);
            Assert.Equal(2, r.RandomOk);
            Assert.Equal((0, 0, 0, 0, 0, 0, 0, 0), (r.PerlinOk, r.PerlinWrong, r.HeightsOk, r.HeightsWrong, r.BiomesWrong, r.ZonesOk, r.ZonesWrong, r.WaterWrong));
            Assert.False(r.AllMatch);
            Assert.Contains(r.Errors, e => e.StartsWith("Line 2 not read", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(bad);
        }
    }

    [Fact]
    public void BrokenRandomRecordsAreReported()
    {
        string bad = Path.Combine(Path.GetTempPath(), "vwe-ref-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        try
        {
            // A value that does not follow from the state, and a Perlin value off by one bit.
            File.WriteAllLines(bad,
            [
                "R init 1 | 00000001 6c078966 714acb3f dbffe6dc",
                "R value 3f000000 | 6c078966 714acb3f dbffe6dc dbfff5aa",
                File.ReadLines(Reference).First(l => l.StartsWith("P ", StringComparison.Ordinal)) is var p ? p[..^1] + (p[^1] == '0' ? '1' : '0') : "",
            ]);
            var r = DumpVerifier.Run(bad, "5DCcdIcuYJ");
            Assert.Equal(1, r.RandomOk);
            Assert.Equal(1, r.RandomWrong);
            Assert.Equal(1, r.PerlinWrong);
            Assert.Contains(r.Errors, e => e.StartsWith("Random: expected [value 3f000000", StringComparison.Ordinal));
            Assert.Contains(r.Errors, e => e.StartsWith("Perlin(", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(bad);
        }
    }
}

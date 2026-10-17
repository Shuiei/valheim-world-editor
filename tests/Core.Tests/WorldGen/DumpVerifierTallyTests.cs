using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The replay's tally: its counts start at nothing, and only the first 25 mismatches are kept.
/// </summary>
public class DumpVerifierTallyTests
{
    [Fact]
    public void ANewTallyCountsNothing()
    {
        var t = new DumpVerifier.Tally();

        Assert.Equal(0, t.RandomOk + t.RandomWrong + t.PerlinOk + t.PerlinWrong + t.HeightsOk + t.HeightsWrong + t.BiomesWrong + t.ZonesOk + t.ZonesWrong + t.WaterWrong + t.Unreadable);
        Assert.Empty(t.Errors);
        Assert.Null(t.Generator);
    }

    [Fact]
    public void MismatchesAreKeptInOrderUpToTwentyFive()
    {
        var t = new DumpVerifier.Tally();

        for (int i = 0; i < 30; i++)
        {
            t.Error($"e{i}");
        }

        Assert.Equal(25, t.Errors.Count);
        Assert.Equal("e0", t.Errors[0]);
        Assert.Equal("e24", t.Errors[24]);
    }
}

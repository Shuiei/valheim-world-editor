using ValheimGen;
using Xunit;

using static WorldEditor.Tests.DumpVerifierSupport;

namespace WorldEditor.Tests;

/// <summary>
/// One Perlin line of the dump compared bit for bit (DumpVerifier.ComparePerlin): a sample the game and the offline noise
/// agree on counts as ok, any other as wrong, with its difference and the expected and the got values named.
/// </summary>
public class DumpVerifierPerlinCompareTests
{
    [Fact]
    public void ASampleTheOfflineNoiseGivesExactlyIsCountedOk()
    {
        var t = new DumpVerifier.Tally();
        float x = 0.5f, y = 0.25f;
        string line = $"P {Hex(x)} {Hex(y)} {Hex(UnityPerlin.Noise(x, y))}";

        DumpVerifier.ComparePerlin(Split(line), t);

        Assert.Equal(1, t.PerlinOk);
        Assert.Equal(0, t.PerlinWrong);
    }

    [Fact]
    public void ASampleTheOfflineNoiseDoesNotGiveIsCountedWrongWithItsDifference()
    {
        var t = new DumpVerifier.Tally();
        float x = 0.5f, y = 0.25f;
        float want = 2f;
        string line = $"P {Hex(x)} {Hex(y)} {Hex(want)}";

        DumpVerifier.ComparePerlin(Split(line), t);

        Assert.Equal(0, t.PerlinOk);
        Assert.Equal(1, t.PerlinWrong);
        Assert.Equal(Math.Abs(UnityPerlin.Noise(x, y) - want), t.PerlinMax, 6);
        Assert.Contains("Perlin(", Assert.Single(t.Errors));
    }
}

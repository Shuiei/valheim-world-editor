using ValheimGen;
using Xunit;

using static WorldEditor.Tests.DumpVerifierSupport;

namespace WorldEditor.Tests;

/// <summary>
/// A replayed Random call: the generator is run the way the game ran it, and its value and state after are compared.
/// </summary>
public class DumpVerifierRandomTests
{
    [Fact]
    public void AMatchingInitCallIsCountedOk()
    {
        var t = new DumpVerifier.Tally();

        DumpVerifier.CompareRandom(Split($"R init 42 | {StateAfterInit(42)}"), t);

        Assert.Equal(1, t.RandomOk);
        Assert.Equal(0, t.RandomWrong);
    }

    [Fact]
    public void AnInitCallWithTheWrongStateIsCountedWrong()
    {
        var t = new DumpVerifier.Tally();

        DumpVerifier.CompareRandom(Split("R init 42 | 00000000 00000000 00000000 00000000"), t);

        Assert.Equal(0, t.RandomOk);
        Assert.Equal(1, t.RandomWrong);
        Assert.Contains("Random: expected", Assert.Single(t.Errors));
    }
}

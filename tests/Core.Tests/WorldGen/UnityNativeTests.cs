using Xunit;
using Rnd = ValheimGen.UnityEngine.Random;

namespace WorldEditor.Tests;

/// <summary>
/// The random number generator's saved state (Random.State): equal for the same four words, whatever draws made it.
/// </summary>
[Collection("World files")]
public class UnityNativeTests
{
    [Fact]
    public void TheSameSeedGivesEqualStatesAndADrawChangesIt()
    {
        Rnd.InitState(42);
        var before = Rnd.state;
        EqualityAsserts.Same(before, Rnd.state);
        Rnd.NextUInt();
        EqualityAsserts.Differ(before, Rnd.state);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void StatesDifferingInOneWordAreNotEqual(int which)
    {
        uint[] w = [1, 2, 3, 4];
        var a = new Rnd.State { s0 = w[0], s1 = w[1], s2 = w[2], s3 = w[3] };
        w[which]++;
        var b = new Rnd.State { s0 = w[0], s1 = w[1], s2 = w[2], s3 = w[3] };
        EqualityAsserts.Differ(a, b);
    }
}

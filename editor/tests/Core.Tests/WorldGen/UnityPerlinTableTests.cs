using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The permutation table behind UnityPerlin (UnityPerlin.BuildPermutation): every byte value once, written twice over
/// so that the lookups never wrap.
/// </summary>
public class UnityPerlinTableTests
{
    private static int[] Table() => (int[])PrivateCall.Static(typeof(UnityPerlin), "BuildPermutation")!;

    [Fact]
    public void TheTableHasFiveHundredTwelveEntries()
    {
        Assert.Equal(512, Table().Length);
    }

    [Fact]
    public void EachByteValueAppearsOnceInTheFirstHalf()
    {
        int[] first = Table()[..256];
        Assert.Equal(Enumerable.Range(0, 256).OrderBy(v => v), first.OrderBy(v => v));
    }

    [Fact]
    public void TheSecondHalfRepeatsTheFirst()
    {
        int[] table = Table();
        Assert.Equal(table[..256], table[256..]);
    }
}

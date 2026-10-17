using ValheimGen;
using Xunit;

using static WorldEditor.Tests.DumpVerifierSupport;

namespace WorldEditor.Tests;

/// <summary>
/// Which dump lines the verifier can read: a Random call with its values and the state after it, a Perlin sample, and
/// the rest by their kind.
/// </summary>
public class DumpVerifierLinesTests
{
    [Fact]
    public void ARandomCallWithItsValuesAndStateIsReadable()
    {
        // R value x | s0 s1 s2 s3: one value for "value", then the bar, then the four state words.
        Assert.True(DumpVerifier.Readable(Split("R value x | 1 2 3 4")));
    }

    [Fact]
    public void ARandomCallWithTheWrongNumberOfValuesIsNot()
    {
        Assert.False(DumpVerifier.Readable(Split("R value | 1 2 3 4")));
    }

    [Fact]
    public void AnUnknownRandomCallIsNotReadable()
    {
        Assert.False(DumpVerifier.Readable(Split("R frobnicate x | 1 2 3 4")));
    }

    [Fact]
    public void APerlinSampleNeedsItsThreeValuesAndKind()
    {
        Assert.True(DumpVerifier.Readable(Split("P 1 2 3")));
        Assert.False(DumpVerifier.Readable(Split("P 1 2")));
    }

    [Fact]
    public void AnUnknownKindNeedsNothing()
    {
        Assert.True(DumpVerifier.Readable(Split("Q")));
    }
}

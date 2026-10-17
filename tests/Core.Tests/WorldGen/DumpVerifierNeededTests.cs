using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How many values each kind of dump line needs, and how many values each Random call takes, as the verifier counts them.
/// </summary>
public class DumpVerifierNeededTests
{
    [Theory]
    [InlineData("init", 1)]
    [InlineData("value", 1)]
    [InlineData("circle", 2)]
    [InlineData("rangei", 3)]
    [InlineData("rangef", 3)]
    [InlineData("nextbool", -1)]
    public void ARandomCallTakesTheValuesItsNameSays(string call, int values)
    {
        Assert.Equal(values, PrivateCall.Static(typeof(DumpVerifier), "RandomValues", call));
    }

    [Theory]
    [InlineData("P", 4)]
    [InlineData("H", 5)]
    [InlineData("Q", 0)]
    public void ALineKindNeedsItsCountOfValues(string kind, int needed)
    {
        Assert.Equal(needed, PrivateCall.Static(typeof(DumpVerifier), "Needed", kind));
    }

    [Fact]
    public void AZoneLineNeedsAValueForEachVertexOfTheGridAfterItsHeader()
    {
        int vertices = (BaseTerrain.Width + 1) * (BaseTerrain.Width + 1);

        Assert.Equal(3 + vertices, PrivateCall.Static(typeof(DumpVerifier), "Needed", "Z"));
    }
}

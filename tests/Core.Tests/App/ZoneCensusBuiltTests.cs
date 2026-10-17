using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether an object added in the session counts as a player-built piece (ZoneCensus.IsBuilt): a builder given in its
/// data decides, and "0" is no builder.
/// </summary>
public class ZoneCensusBuiltTests
{
    /// <summary>An added object with a builder given in its data.</summary>
    private static NewObject WithCreator(string value) =>
        new(-1, 7, Vector3.Zero, Vector3.Zero, 0f, Data: [new ObjectField("longs", ObjectField.CreatorKey, value)]);

    [Fact]
    public void AGivenBuilderMakesTheObjectABuiltPiece()
    {
        Assert.True((bool)PrivateCall.Static(typeof(ZoneCensus), "IsBuilt", null, WithCreator("123"))!);
    }

    [Fact]
    public void AGivenBuilderOfZeroMakesTheObjectNoBuiltPiece()
    {
        Assert.False((bool)PrivateCall.Static(typeof(ZoneCensus), "IsBuilt", null, WithCreator("0"))!);
    }
}

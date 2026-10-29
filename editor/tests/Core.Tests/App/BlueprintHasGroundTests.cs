using System.Text.Json;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether a blueprint clip has ground in it: its "rel" heights hold at least one that is not null and not the -32768
/// the editor writes for no ground.
/// </summary>
public sealed class BlueprintHasGroundTests
{
    /// <summary>Reads a clip's JSON and asks whether it has ground.</summary>
    private static bool HasGround(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return (bool)PrivateMember.Call(typeof(BlueprintStore), "HasGround", null, doc.RootElement)!;
    }

    /// <summary>A clip with a height in its "rel" list has ground.</summary>
    [Fact]
    public void AHeightInTheListMeansGround() =>
        Assert.True(HasGround("{\"rel\":[null,5]}"));

    /// <summary>The -32768 marker and nulls alone are no ground.</summary>
    [Fact]
    public void OnlyNullAndTheMarkerMeanNoGround() =>
        Assert.False(HasGround("{\"rel\":[null,-32768]}"));

    /// <summary>A zero height is ground: only -32768 is the marker.</summary>
    [Fact]
    public void AZeroHeightIsStillGround() =>
        Assert.True(HasGround("{\"rel\":[-32768,0]}"));

    /// <summary>An empty list has no ground.</summary>
    [Fact]
    public void AnEmptyListHasNoGround() =>
        Assert.False(HasGround("{\"rel\":[]}"));

    /// <summary>A clip without "rel" has no ground.</summary>
    [Fact]
    public void AClipWithoutRelHasNoGround() =>
        Assert.False(HasGround("{}"));

    /// <summary>A "rel" that is not a list has no ground.</summary>
    [Fact]
    public void ARelThatIsNotAListHasNoGround() =>
        Assert.False(HasGround("{\"rel\":5}"));
}

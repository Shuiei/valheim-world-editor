using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What a world search has found so far (WorldSearchTally): the counts, the limit on the hits kept, the query's
/// matching, and the names shown.
/// </summary>
public class WorldSearchTallyTests
{
    [Fact]
    public void FoundAddsItsAmountToItsCount()
    {
        var tally = new WorldSearchTally("x", 10);
        tally.Found(1, "n", Vector3.Zero, null, "k", 2);
        tally.Found(2, "n", Vector3.Zero, null, "k", 3);
        Assert.Equal(5, tally.Finish().Counts["k"]);
    }

    [Fact]
    public void TheHitsKeptAreCappedAtTheLimit()
    {
        var tally = new WorldSearchTally("x", 1);
        tally.Found(1, "n", Vector3.Zero, null, "k");
        tally.Found(2, "n", Vector3.Zero, null, "k");
        var result = tally.Finish();
        Assert.Single(result.Hits);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public void TruncatedIsSetWhenHitsWereLeftOut()
    {
        var tally = new WorldSearchTally("x", 1);
        tally.Found(1, "n", Vector3.Zero, null, "k");
        tally.Found(2, "n", Vector3.Zero, null, "k");
        Assert.True(tally.Finish().Truncated);
    }

    [Fact]
    public void MatchingIgnoresCase()
    {
        Assert.True(new WorldSearchTally("AbC", 1).Matches("xabcx"));
    }

    [Fact]
    public void MatchingIsFalseWhenTheQueryIsAbsent()
    {
        Assert.False(new WorldSearchTally("zzz", 1).Matches("abc"));
    }

    [Fact]
    public void BookkeepingNamesAreHiddenUnlessTheQueryStartsWithAnUnderscore()
    {
        Assert.False(new WorldSearchTally("zone", 1).Shown("_ZoneCtrl"));
        Assert.True(new WorldSearchTally("_zo", 1).Shown("_ZoneCtrl"));
    }

    [Fact]
    public void AFinishWithoutHitsIsEmpty()
    {
        var result = new WorldSearchTally("x", 5).Finish();
        Assert.Empty(result.Hits);
        Assert.Equal(0, result.Total);
    }
}

using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What a contents search matches in one object's texts (WorldSearchMatch.Texts): the first matching text is shown, and
/// an object counts once.
/// </summary>
public class WorldSearchMatchTests
{
    [Fact]
    public void TheFirstMatchingTextIsTheMatch()
    {
        var z = new ZdoData { Position = new Vector3(1, 2, 3) };
        z.StringList.Add((1, "hello world"));
        z.StringList.Add((2, "hello again"));
        var tally = new WorldSearchTally("hello", 10);
        WorldSearchMatch.Texts(tally, 7, "Sign", z);
        var hit = Assert.Single(tally.Finish().Hits);
        Assert.EndsWith(": hello world", hit.Match);
    }

    [Fact]
    public void AnObjectWithSeveralMatchingTextsCountsOnce()
    {
        var z = new ZdoData();
        z.StringList.Add((1, "hello"));
        z.StringList.Add((2, "hello"));
        var tally = new WorldSearchTally("hello", 10);
        WorldSearchMatch.Texts(tally, 7, "Sign", z);
        Assert.Equal(1, tally.Finish().Total);
    }

    [Fact]
    public void AnObjectWithNoMatchingTextAddsNothing()
    {
        var z = new ZdoData();
        z.StringList.Add((1, "other"));
        var tally = new WorldSearchTally("hello", 10);
        WorldSearchMatch.Texts(tally, 7, "Sign", z);
        Assert.Equal(0, tally.Finish().Total);
    }
}

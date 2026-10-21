using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A new world's request is checked before anything is written: the name, the seed, the radius, and a bare zone that
/// needs generated zones around it.
/// </summary>
public class WorldCreatorRequestTests
{
    [Fact]
    public void AnEmptyNameIsRefused()
    {
        Assert.Throws<ArgumentException>(() => WorldCreator.CheckRequest("", "seed", 0, false));
    }

    [Fact]
    public void AnEmptySeedIsRefused()
    {
        Assert.Throws<ArgumentException>(() => WorldCreator.CheckRequest("world", "", 0, false));
    }

    [Fact]
    public void ANegativeRadiusIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldCreator.CheckRequest("world", "seed", -1, false));
    }

    [Fact]
    public void ABareZoneNeedsARadiusOfTwoOrMore()
    {
        Assert.Throws<ArgumentException>(() => WorldCreator.CheckRequest("world", "seed", 1, true));
    }

    [Fact]
    public void AValidRequestPasses()
    {
        WorldCreator.CheckRequest("world", "seed", 2, true);
    }
}

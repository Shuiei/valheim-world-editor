using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A 1 m detail window: built at its size, and kept while the edits are the same.
/// </summary>
public class MapDataDetailTests
{
    [Fact]
    public void ABuiltWindowHasTheRequestedSize()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = MapDataTestWorld.Open(w);
        var map = new MapData(terrain, edits);

        var window = map.BuildDetail(0, 0, 8);

        Assert.Equal(8, window.Width);
        Assert.Equal(8, window.Height);
        Assert.Equal(64, window.Heights.Length);
    }

    [Fact]
    public void AWindowOfTheSameEditsIsKept()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = MapDataTestWorld.Open(w);
        var map = new MapData(terrain, edits);

        var first = map.Detail(0, 0, 8, 1);

        Assert.Same(first, map.Detail(0, 0, 8, 1));
    }

    [Fact]
    public void AWindowOfNewerEditsIsBuiltAgain()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = MapDataTestWorld.Open(w);
        var map = new MapData(terrain, edits);
        var first = map.Detail(0, 0, 8, 1);

        var newer = map.Detail(0, 0, 8, 2);

        Assert.NotSame(first, newer);
    }
}

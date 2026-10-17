using System.Numerics;
using TerrainEditor.Terrain;
using Xunit;

using static WorldEditor.Tests.TerrainModifierSupport;

namespace WorldEditor.Tests;

/// <summary>
/// The modifiers' order and zones: the game's sort order, which zones a modifier's reach touches, and degrees to radians.
/// </summary>
public class TerrainModifiersZonesTests
{
    /// <summary>A modifier set with no placed objects, to index the zones of the modifiers given.</summary>
    private static TerrainModifiers Empty() => new([]);

    [Fact]
    public void ANonPlayerModifierComesBeforeAPlayerOneWhateverItsSort()
    {
        var list = new List<TerrainModifiers.Modifier> { Make(Vector3.Zero, sortOrder: 0, player: true), Make(Vector3.Zero, sortOrder: 9) };

        TerrainModifiers.SortByModifiers(list);

        Assert.False(list[0].Player);
        Assert.True(list[1].Player);
    }

    [Fact]
    public void AmongNonPlayerModifiersTheSortOrderDecides()
    {
        var list = new List<TerrainModifiers.Modifier> { Make(Vector3.Zero, sortOrder: 2), Make(Vector3.Zero, sortOrder: 1) };

        TerrainModifiers.SortByModifiers(list);

        Assert.Equal(1, list[0].SortOrder);
        Assert.Equal(2, list[1].SortOrder);
    }

    [Fact]
    public void AtTheSameSortOrderTheEarlierCreatedComesFirst()
    {
        var list = new List<TerrainModifiers.Modifier> { Make(Vector3.Zero, time: 5), Make(Vector3.Zero, time: 1) };

        TerrainModifiers.SortByModifiers(list);

        Assert.Equal(1, list[0].CreationTime);
        Assert.Equal(5, list[1].CreationTime);
    }

    [Fact]
    public void AModifierIsIndexedInEveryZoneItsReachTouches()
    {
        var m = Empty();

        m.IndexByZone([Make(Vector3.Zero, level: true, levelRadius: 1)]);

        Assert.Single(m.InZone(0, 0));
        Assert.Empty(m.InZone(1, 0));
    }

    [Fact]
    public void AModifierNearAZoneEdgeReachesTheNeighbourZone()
    {
        Assert.True(TerrainModifiers.Overlaps(Make(new Vector3(30, 0, 0)), 1, 0));
    }

    [Fact]
    public void AModifierFarFromAZoneDoesNotReachIt()
    {
        Assert.False(TerrainModifiers.Overlaps(Make(new Vector3(0, 0, 0)), 1, 0));
    }

    [Fact]
    public void DegreesBecomeRadians()
    {
        Assert.Equal(MathF.PI, TerrainModifiers.Deg(180f), 5);
        Assert.Equal(0f, TerrainModifiers.Deg(0f));
    }
}

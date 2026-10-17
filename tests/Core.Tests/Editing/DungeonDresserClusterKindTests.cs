using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The cluster kinds, each dressed by hand at the middle of a clear room: every kind starts with a piece at its anchor,
/// and a kind with no dressing places nothing.
/// </summary>
public class DungeonDresserClusterKindTests
{
    [Theory]
    [InlineData("camp")]
    [InlineData("meal")]
    [InlineData("scribe")]
    [InlineData("stores")]
    [InlineData("armoury")]
    [InlineData("altar")]
    [InlineData("bones")]
    [InlineData("workshop")]
    public void EachClusterKindPlacesAPieceAtItsAnchor(string kind)
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = new DungeonOut();
        DungeonDresser.DressClusterAs(room.Frame, kind, room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        Assert.NotEmpty(DungeonDresserSupport.At(o, room.Frame.W(0, 0, 0)));
    }

    [Fact]
    public void AClusterKindWithNoDressingPlacesNothing()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = new DungeonOut();
        DungeonDresser.DressClusterAs(room.Frame, "no such kind", room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        Assert.Empty(o.Items);
    }
}

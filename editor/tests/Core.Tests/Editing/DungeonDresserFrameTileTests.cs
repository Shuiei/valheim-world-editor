using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world tiles of a room's frame (DungeonDresser.Frame.TileAt and Has): a frame's own (across, deep) coordinates
/// map to the world tile for each way the room faces, and a tile is the room's when the room holds it.
/// </summary>
public class DungeonDresserFrameTileTests
{
    /// <summary>A four-wide, six-deep hall whose box starts at tile (10, 20).</summary>
    private static DungeonPlan.Space Hall(int dirX, int dirZ) =>
        new() { X0 = 10, Z0 = 20, W = 4, H = 6, Dir = (dirX, dirZ), Room = DungeonKit.Room.Hall };

    private static DungeonDresser.Frame FrameOf(DungeonPlan.Space s)
    {
        var mat = DungeonKit.MaterialOf(DungeonKit.BiomeOf("Black Forest").Material);
        return new DungeonDresser.Frame(s, mat, new DungeonPlan.Level { Index = 0 });
    }

    [Fact]
    public void ARoomFacingPlusDeepCountsAcrossXAndDeepZFromItsCorner()
    {
        var frame = FrameOf(Hall(0, 1));
        Assert.Equal((11, 23), frame.TileAt(1, 3));
    }

    [Fact]
    public void ARoomFacingMinusDeepCountsFromTheFarCorner()
    {
        var frame = FrameOf(Hall(0, -1));
        Assert.Equal((13, 25), frame.TileAt(0, 0));
    }

    [Fact]
    public void ARoomFacingPlusXCountsDeepAlongXAndAcrossBackFromZ()
    {
        var frame = FrameOf(Hall(1, 0));
        Assert.Equal((10, 25), frame.TileAt(0, 0));
        Assert.Equal((11, 23), frame.TileAt(2, 1));
    }

    [Fact]
    public void ARoomFacingMinusXCountsDeepBackFromTheFarSide()
    {
        var frame = FrameOf(Hall(-1, 0));
        Assert.Equal((12, 22), frame.TileAt(2, 1));
    }

    [Fact]
    public void HasIsTrueOnlyForATileTheRoomHolds()
    {
        var s = Hall(0, 1);
        s.Tiles.Add((11, 23));
        var frame = FrameOf(s);
        Assert.True(frame.Has(1, 3));
        Assert.False(frame.Has(0, 0));
    }
}

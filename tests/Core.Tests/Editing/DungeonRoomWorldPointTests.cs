using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A point of a dungeon room's own frame in world space: turned by the room's rotation, then moved to its position.
/// </summary>
public class DungeonRoomWorldPointTests
{
    [Fact]
    public void AnUnturnedRoomsPointIsItsPositionPlusItsOffset()
    {
        var room = new Dungeons.Placed(1, new Vector3(10, 2, 5), Quaternion.Identity);
        Assert.Equal(new Vector3(11, 4, 8), room.ToWorld(new Vector3(1, 2, 3)));
    }

    [Fact]
    public void AQuarterTurnToTheRightTurnsTheOffsetBeforeMovingIt()
    {
        // A quarter turn about +Y takes the room's +X offset to -Z.
        var room = new Dungeons.Placed(1, new Vector3(10, 2, 5), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));
        Vector3 p = room.ToWorld(new Vector3(1, 0, 0));
        Assert.Equal(10f, p.X, 4);
        Assert.Equal(2f, p.Y, 4);
        Assert.Equal(4f, p.Z, 4);
    }
}

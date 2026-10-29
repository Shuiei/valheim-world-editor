using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How the dungeons' hangings go on a wall: a long cloth only in rooms tall enough for it, and nothing for a prop that
/// is not a hanging.
/// </summary>
public class DungeonPropsTests
{
    [Fact]
    public void ALongClothHangsOnlyInATallRoom()
    {
        Assert.Equal((-0.25f, 0.1f, 0f), DungeonProps.Hanging("cloth_hanging_long", 10));
        Assert.Null(DungeonProps.Hanging("cloth_hanging_long", 4));
        // A banner goes just under the ceiling.
        Assert.Equal((9.9f, 0.2f, 90f), DungeonProps.Hanging("piece_banner01", 10));
        Assert.Null(DungeonProps.Hanging("Beech1", 10));
    }
}

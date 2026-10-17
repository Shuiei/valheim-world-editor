using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How much a room's purpose lights it: the grand rooms closer lit, the rooms of the dead and the cells dimmer, and any
/// other room at the usual 1.
/// </summary>
public class DungeonDresserLightOfTests
{
    [Theory]
    [InlineData(DungeonKit.Room.Entrance, 0.75f)]
    [InlineData(DungeonKit.Room.Throne, 0.75f)]
    [InlineData(DungeonKit.Room.Boss, 0.75f)]
    [InlineData(DungeonKit.Room.Tomb, 1.5f)]
    [InlineData(DungeonKit.Room.Cells, 1.5f)]
    [InlineData(DungeonKit.Room.Hall, 1f)]
    public void EachRoomPurposeHasItsLightLevel(DungeonKit.Room room, float light)
    {
        Assert.Equal(light, PrivateMember.Call<float>(typeof(DungeonDresser), "LightOf", null, room));
    }
}

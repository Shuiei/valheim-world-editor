using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The table Furnish dresses each room kind from: every kind has a dresser named after it, both kinds of stairwell share
/// the stairwell's dresser, and a kind with no entry is left alone.
/// </summary>
public class DungeonDresserRoomTableTests
{
    private static IReadOnlyDictionary<DungeonKit.Room, Action<DungeonDresser.Frame>> Table()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        return DungeonDresser.RoomDresserTable(room.Material, room.Biome, room.Style, room.Settings, new Random(1), new DungeonOut());
    }

    [Fact]
    public void EveryRoomKindHasADresser()
    {
        var table = Table();
        Assert.Equal(Enum.GetValues<DungeonKit.Room>().OrderBy(r => r), table.Keys.OrderBy(r => r));
    }

    [Fact]
    public void EachRoomKindIsDressedByTheDresserNamedAfterIt()
    {
        var table = Table();
        Assert.All(table, pair =>
        {
            string expected = pair.Key is DungeonKit.Room.StairTop or DungeonKit.Room.StairBottom ? "Stairwell" : pair.Key.ToString();
            Assert.Equal(expected, pair.Value.Method.Name);
        });
    }

    [Fact]
    public void BothStairwellKindsShareTheStairwellDresser()
    {
        var table = Table();
        Assert.Equal("Stairwell", table[DungeonKit.Room.StairTop].Method.Name);
        Assert.Equal("Stairwell", table[DungeonKit.Room.StairBottom].Method.Name);
    }

    [Fact]
    public void AKindWithNoEntryIsLeftAlone()
    {
        Assert.False(Table().ContainsKey((DungeonKit.Room)999));
    }
}

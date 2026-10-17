using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The throne a dungeon's dais holds, by the room's material: marble and grausten get the black marble throne, goblin
/// the bone one of the goblin, and stave the moose throne.
/// </summary>
public class DungeonDresserThroneTests
{
    /// <summary>The throne the dresser picks for a room of this material, in the Black Forest's crypt style.</summary>
    private static string PickThroneOf(string material)
    {
        var biome = DungeonKit.BiomeOf("Black Forest");
        var style = DungeonKit.StyleOf("Crypt");
        var settings = new DungeonGen.Settings(Biome: biome.Name, Style: style.Name, Size: 3, Levels: 1, BossKey: false, Seed: 1);
        var dresser = (DungeonDresser)PrivateCall.Create(typeof(DungeonDresser), [typeof(DungeonKit.Material), typeof(DungeonKit.Biome), typeof(DungeonKit.Style), typeof(DungeonGen.Settings), typeof(Random), typeof(DungeonOut), typeof(string)],
            DungeonKit.MaterialOf(material), biome, style, settings, new Random(1), new DungeonOut(), "Test");
        return (string)PrivateCall.Instance(dresser, "PickThrone")!;
    }

    [Theory]
    [InlineData("Marble")]
    [InlineData("Grausten")]
    public void MarbleAndGraustenGetTheBlackMarbleThrone(string material)
    {
        Assert.Equal("piece_blackmarble_throne", PickThroneOf(material));
    }

    [Fact]
    public void AGoblinRoomGetsTheBoneThrone()
    {
        Assert.Equal("piece_bone_throne", PickThroneOf("Goblin"));
    }

    [Fact]
    public void AStaveRoomGetsTheMooseThrone()
    {
        Assert.Equal("piece_moose_throne", PickThroneOf("Stave"));
    }
}

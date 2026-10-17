using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The pieces a corridor's walls are dressed with: the spikes, the stakes and the bones along the two side walls
/// (DungeonDresser.CorridorSpikes, CorridorStakes and CorridorBones, reached through DressCorridor).
/// </summary>
public class DungeonDresserCorridorWallsTests
{
    /// <summary>The objects a corridor kind puts in a room, as the dresser places them.</summary>
    private static List<DungeonGen.Item> Dress(DressingRoom room, string kind)
    {
        var o = new DungeonOut();
        DungeonDresser.DressCorridor(room.Frame, kind, room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        return o.Items;
    }

    [Fact]
    public void SpikesStandOnTheWallsWithTheWallSpikesPiece()
    {
        var items = Dress(DungeonDresserSupport.ClearRoom("Crypt"), "spikes");

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("Ashlands_Fortress_Wall_Spikes", i.Prefab));
    }

    [Fact]
    public void StakesStandAlongBothSideWallsAtTheWallLine()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var stakes = Dress(room, "stakes");
        var across = stakes.Select(i => room.Frame.Local(i.Position.X, i.Position.Z).U).ToList();

        Assert.NotEmpty(stakes);
        Assert.All(stakes, i => Assert.Equal("piece_sharpstakes", i.Prefab));
        Assert.Contains(across, u => u < 0);
        Assert.Contains(across, u => u > 0);
        Assert.All(across, u => Assert.InRange(MathF.Abs(u), room.Frame.HalfA - 0.91f, room.Frame.HalfA - 0.89f));
    }

    [Fact]
    public void BonesLieAgainstTheSideWallsAndAreBonesOrSkulls()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var bones = Dress(room, "bones");
        string[] names = ["skull_pile", "bone_stack", "LargeBone"];

        Assert.NotEmpty(bones);
        Assert.All(bones, i => Assert.Contains(i.Prefab, names));
        Assert.All(bones, i => Assert.InRange(MathF.Abs(room.Frame.Local(i.Position.X, i.Position.Z).U), room.Frame.HalfA - 0.56f, room.Frame.HalfA - 0.54f));
    }
}

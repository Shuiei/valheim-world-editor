using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What each kind of corridor dressing puts in a corridor (DungeonDresser.Sets.Corridors): bones, vines, spikes, roots,
/// stakes, creeping eggs and icicles, each made of its own objects.
/// </summary>
public class DungeonCorridorKindTests
{
    private static readonly string[] Expected4 = ["skull_pile", "bone_stack", "LargeBone"];
    private static readonly string[] Expected3 = ["root11", "root12", "root07"];
    private static readonly string[] Expected2 = ["CreepProp_wall01", "CreepProp_egg_hanging01", "CreepProp_egg_hanging02", "SeekerEgg"];
    private static readonly string[] Expected1 = ["caverock_ice_stalagtite", "BlackIceShard_01"];
    private static readonly int[] Seeds = [1, 2, 3, 4, 5, 6];

    [Fact]
    public void BonesLieAlongTheWallsOfACatacomb()
    {
        var items = DungeonCorridorSupport.Dress("bones", Seeds);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Contains(i.Prefab, Expected4));
    }

    [Fact]
    public void VinesHangOnTheWalls()
    {
        var items = DungeonCorridorSupport.Dress("vines", Seeds);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("vines", i.Prefab));
    }

    [Fact]
    public void SpikesStandOnTheAshlandsWalls()
    {
        var items = DungeonCorridorSupport.Dress("spikes", Seeds);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("Ashlands_Fortress_Wall_Spikes", i.Prefab));
    }

    [Fact]
    public void RootsGrowOnTheWoodsWalls()
    {
        var items = DungeonCorridorSupport.Dress("roots", Seeds);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Contains(i.Prefab, Expected3));
    }

    [Fact]
    public void StakesLineTheGoblinWarrensWalls()
    {
        var items = DungeonCorridorSupport.Dress("stakes", Seeds);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("piece_sharpstakes", i.Prefab));
    }

    [Fact]
    public void CreepHasItsEggsAndAFloorEgg()
    {
        var items = DungeonCorridorSupport.Dress("creep", Seeds);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Contains(i.Prefab, Expected2));
    }

    [Fact]
    public void IciclesHangFromTheCeilingOfACorridor()
    {
        // Every corridor at least four metres across gets at least one icicle from the ceiling.
        var items = DungeonCorridorSupport.Dress("icicles", Seeds);
        Assert.Contains(items, i => i.Prefab == "caverock_ice_stalagtite");
        Assert.All(items, i => Assert.Contains(i.Prefab, Expected1));
    }
}

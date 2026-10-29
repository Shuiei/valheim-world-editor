using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Which objects shape the ground as the game runs: location proxies, and the prefabs the save lists as carrying terrain
/// modifiers (WorldSave.ModifierPrefabs). Any other object does not.
/// </summary>
public class WorldSaveDropsShapesTests
{
    private static bool Shapes(int prefab) => PrivateMember.Call<bool>(typeof(WorldSave), "Shapes", null, prefab);

    [Fact]
    public void ALocationProxyShapesTheGround()
    {
        Assert.True(Shapes(WorldSave.LocationProxyPrefab));
    }

    [Fact]
    public void AListedModifierPrefabShapesTheGround()
    {
        var before = WorldSave.ModifierPrefabs;
        try
        {
            int modifier = StableHash.Of("ExampleTerrainModifier");
            WorldSave.ModifierPrefabs = [modifier];
            Assert.True(Shapes(modifier));
        }
        finally
        {
            WorldSave.ModifierPrefabs = before;
        }
    }

    [Fact]
    public void AnObjectThatIsNeitherAProxyNorListedDoesNotShapeTheGround()
    {
        var before = WorldSave.ModifierPrefabs;
        try
        {
            WorldSave.ModifierPrefabs = [StableHash.Of("ExampleTerrainModifier")];
            Assert.False(Shapes(StableHash.Of("Stone")));
        }
        finally
        {
            WorldSave.ModifierPrefabs = before;
        }
    }
}

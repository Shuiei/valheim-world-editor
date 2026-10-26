using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>The game's own world rules and objects as extensions see them (GameWorldData): the editor's, as they are each time.</summary>
public class GameWorldDataTests
{
    [Fact]
    public void TheGamesRulesAndObjectsAreTheEditors()
    {
        var game = GameWorldData.Instance;

        Assert.Equal(SeedLocations.Available, game.LocationsRead);
        Assert.Same(SeedLocations.Current, game.Locations);
        Assert.Same(Regrow.GameRules, game.Vegetation);
        foreach (string name in new[] { "Beech1", "no_such_prefab" })
        {
            Assert.Equal(PrefabCatalog.Get(StableHash.Of(name)) != null, game.HasPrefab(name));
        }
        foreach (string name in new[] { "sunkencrypt_Entrance", "no_such_room" })
        {
            Assert.Equal(TerrainEditor.Editing.Dungeons.IsGameRoom(name), game.IsGameRoom(name));
        }
        Assert.False(game.HasPrefab("no_such_prefab"));
        Assert.False(game.IsGameRoom("no_such_room"));
    }
}

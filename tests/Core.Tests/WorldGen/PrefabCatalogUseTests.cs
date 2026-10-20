using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Taking the catalogue from its JSON: the kinds it offers and the items, from the game's own copy in the fixtures
/// (taking it again leaves the same catalogue for the other tests).
/// </summary>
public class PrefabCatalogUseTests
{
    [Fact]
    public void TakingTheCatalogueOffersItsPlaceableKinds()
    {
        PrefabCatalog.Use(File.ReadAllText(Path.Combine(Fixtures.Root, "prefabs.json")));

        Assert.NotNull(PrefabCatalog.Get(Fixtures.Hash("sapling_turnip")));
        Assert.Null(PrefabCatalog.Get(Fixtures.Hash("Boar")));
        Assert.True(PrefabCatalog.Placeable.Count() > 500);
        // Whether its network view keeps its first scale.
        Assert.True(PrefabCatalog.Get(Fixtures.Hash("Bush01"))!.SyncScale);
        Assert.False(PrefabCatalog.Get(Fixtures.Hash("Pickable_Stone"))!.SyncScale);
    }
}

using Xunit;

namespace WorldEditor.Tests;

/// <summary>The fake hosts' world data (NoWorldGameData): nothing of the game's, and reading it does nothing.</summary>
public class NoWorldGameDataTests
{
    [Fact]
    public async Task ItHoldsNothingOfTheGames()
    {
        var none = new NoWorldGameData();

        await none.ReadLocations();

        Assert.False(none.LocationsRead);
        Assert.Empty(none.Locations.Locations);
        Assert.Empty(none.Locations.AltBiomes);
        Assert.Empty(none.Vegetation);
        Assert.False(none.HasPrefab("Beech1"));
        Assert.False(none.IsGameRoom("sunkencrypt_Entrance"));
    }
}

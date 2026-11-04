using TerrainEditor.App;
using ValheimGen;
using Xunit;
using K = TerrainEditor.App.MasterMap.Kind;

namespace WorldEditor.Tests;

/// <summary>
/// MasterMapChecks: the drawing's own warnings (the start on water or off the Meadows, a biome not drawn, a Black
/// Forest without coast for the tin) and the world's (dungeons none or few of, no Sunken Crypts for the iron, no Fuling
/// villages for the black metal).
/// </summary>
public class MasterMapChecksTests
{
    private static string Keys(IEnumerable<Said> said) => string.Join(" | ", said.Select(s => s.English));

    [Fact]
    public void AStartOnWaterIsSaid()
    {
        var map = new MasterMap { Start = (100, 100) };

        Assert.Contains("start point is on water", Keys(MasterMapChecks.OfMap(map)));
    }

    [Fact]
    public void TheMiddleOffTheMeadowsIsSaidWhenNoStartIsDrawn()
    {
        var map = new MasterMap();
        map.Disc(MasterMap.Size / 2, MasterMap.Size / 2, 50, K.Plains);

        Assert.Contains("middle of the world is Plains", Keys(MasterMapChecks.OfMap(map)));
    }

    [Fact]
    public void ABiomeNotDrawnIsSaidWithWhatTheGameKeepsThere()
    {
        var map = new MasterMap();
        map.Disc(1024, 1024, 100, K.Meadows);

        string said = Keys(MasterMapChecks.OfMap(map));

        Assert.Contains("No Mountain drawn: no Moder, silver, obsidian or Frost caves.", said);
        Assert.DoesNotContain("No Meadows", said);
        Assert.DoesNotContain("Deep North", said);
    }

    [Fact]
    public void ABlackForestAwayFromWaterHasNoTinAndOneOnTheCoastHas()
    {
        var map = new MasterMap();
        map.Disc(1024, 1024, 200, K.Meadows);
        map.Disc(1024, 1024, 50, K.BlackForest);

        Assert.Contains("tin grows only along Black Forest shores", Keys(MasterMapChecks.OfMap(map)));

        map.Disc(1024, 1024, 5, K.Lake);
        Assert.DoesNotContain("Black Forest touches water", Keys(MasterMapChecks.OfMap(map)));
    }

    [Fact]
    public void TheWorldSaysDungeonsNoneOrFewAndTheMissingMetals()
    {
        var rules = new LocationRuleSet();
        rules.Locations.Add(new LocationRule { Prefab = "MountainCave02", Enable = 1, Quantity = 40 });
        rules.Locations.Add(new LocationRule { Prefab = "TrollCave02", Enable = 1, Quantity = 20 });
        rules.Locations.Add(new LocationRule { Prefab = "SunkenCrypt4", Enable = 1, Quantity = 100 });
        rules.Locations.Add(new LocationRule { Prefab = MasterMapChecks.FulingVillage, Enable = 1, Quantity = 30 });

        string said = Keys(MasterMapChecks.OfWorld([("MountainCave02", 3), ("TrollCave02", 18), (MasterMapChecks.FulingVillage, 5)], rules));

        Assert.Contains("Few Frost caves: 3 of the 40", said);
        Assert.DoesNotContain("Troll caves", said);
        Assert.Contains("No Sunken Crypts", said);
        Assert.Contains("no iron", said);
        Assert.DoesNotContain("black metal", said);
    }
}

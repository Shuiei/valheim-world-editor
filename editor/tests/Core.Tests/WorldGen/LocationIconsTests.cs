using ValheimGen;
using Xunit;
using NVector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// The icons the map shows for a world's locations: the game's (always, or once the zone is made) and Expand World
/// Data's (an icon of the kind's name or another, its size).
/// </summary>
public class LocationIconsTests
{
    /// <summary>One instance of each kind at x = its index, placed or not.</summary>
    private static List<LocationIcons.Icon> Icons(bool placed, params LocationRule[] kinds) =>
        LocationIcons.Of(kinds.Select((k, i) => (TerrainEditor.Save.StableHash.Of(k.Prefab), new NVector3(i, 0, 0), placed)), kinds);

    [Fact]
    public void TheGamesKindsShowTheirIconAlwaysOrOncePlaced()
    {
        LocationRule start = new() { Prefab = "StartTemple", IconAlways = 1 }, trader = new() { Prefab = "Vendor_BlackForest", IconPlaced = 1 }, crypt = new() { Prefab = "Crypt2" };
        Assert.Equal([new LocationIcons.Icon(0, 0, "StartTemple", 1f)], Icons(false, start, trader, crypt));
        Assert.Equal(["StartTemple", "Vendor_BlackForest"], Icons(true, start, trader, crypt).Select(i => i.Name));
        // An instance of no kind of the world shows nothing.
        Assert.Empty(LocationIcons.Of([(12345, NVector3.Zero, true)], [start]));
    }

    [Fact]
    public void TheModsNamesChooseTheIconAndItsSize()
    {
        var kind = new LocationRule { Prefab = "MyHut", IconAlwaysName = "Hildir_camp, 2, true", IconPlacedName = "true" };
        // Placed: the placed icon (the kind's own name); not yet: the always one, with its size.
        Assert.Equal(new LocationIcons.Icon(0, 0, "MyHut", 1f), Assert.Single(Icons(true, kind)));
        Assert.Equal(new LocationIcons.Icon(0, 0, "Hildir_camp", 2f), Assert.Single(Icons(false, kind)));
        // "false" or nothing: no icon, whatever the game's flags say; a size of 0 or less is the game's.
        Assert.Empty(Icons(true, new LocationRule { Prefab = "A", IconAlways = 1, IconAlwaysName = "false", IconPlacedName = "" }));
        Assert.Equal(1f, Assert.Single(Icons(false, new LocationRule { Prefab = "B", IconAlwaysName = "StartTemple, 0" })).Size);
    }
}
